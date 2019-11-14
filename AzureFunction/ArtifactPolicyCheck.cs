namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Models;
    using Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Request;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.Services.WebApi;
    using Newtonsoft.Json;

    using WebJobsExecutionContext = WebJobs.ExecutionContext;

    public static class ArtifactPolicyCheck
    {
        private const int NumberOfHttpRetries = 5;

        [FunctionName("ArtifactPolicyCheck")]
        public async static Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]HttpRequest req,
            ILogger log,
            WebJobsExecutionContext executionContext)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            log.LogInformation("function directory: " + executionContext.FunctionDirectory);
            log.LogInformation("function app directory: " + executionContext.FunctionAppDirectory);

            EvaluationRequest request;
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync().ConfigureAwait(false);
                request = JsonConvert.DeserializeObject<EvaluationRequest>(requestBody);
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult(string.Format(
                    CultureInfo.InvariantCulture,
                    "Request body is invalid. Encountered error : {0}",
                    e.ToString()));
            }

            string imageProvenance = JsonConvert.SerializeObject(request.ImageProvenance);

            if (string.IsNullOrWhiteSpace(imageProvenance))
            {
                return new BadRequestObjectResult("Image provenance is empty");
            }

            if (string.IsNullOrWhiteSpace(request.PolicyData))
            {
                return new BadRequestObjectResult("Policy data is empty");
            }

            if (!string.IsNullOrWhiteSpace(request.AuthToken))
            {
                TaskProperties taskProperties = Utilities.CreateTaskProperties(request);
                new Thread(async () => await ExecuteUsingTimelineLogs(
                  executionContext,
                  log,
                  imageProvenance,
                  request.PolicyData,
                  request.CheckSuiteId,
                  taskProperties,
                  request.Variables));
                return new NoContentResult();
            }
            else
            {
                string outputLog;
                var violations = Utilities.ExecutePolicyCheck(executionContext, log, imageProvenance, request.PolicyData, null, request.Variables, out outputLog);
                return new OkObjectResult(new EvaluationResponse { Violations = violations.ToList(), Logs = outputLog });
            }
        }

        private static async Task ExecuteUsingTimelineLogs(
            WebJobsExecutionContext executionContext,
            ILogger log,
            string imageProvenance,
            string policy,
            Guid checkSuiteId,
            TaskProperties taskProperties,
            IDictionary<string, string> variables)
        {
            using (var taskClient = new TaskClient(taskProperties))
            {
                var taskLogger = new TaskLogger(taskProperties, taskClient);
                try
                {
                    // create timelinerecord if not provided
                    await taskLogger.CreateTaskTimelineRecordIfRequired(taskClient, default(CancellationToken)).ConfigureAwait(false);

                    // report task started
                    string taskStartedLog = string.Format("Initializing evaluation. Execution id - {0}", executionContext.InvocationId);
                    Utilities.LogInformation(taskStartedLog, log, taskLogger, variables);

                    string outputLog;
                    var violations = Utilities.ExecutePolicyCheck(executionContext, log, imageProvenance, policy, taskLogger, variables, out outputLog);
                    bool succeeded = violations?.Any() == true;
                    Utilities.LogInformation($"Policy check succeeded: {succeeded}", log, taskLogger, variables);

                    await UpdateCheckSuiteResult(
                        taskProperties.PlanUrl,
                        taskProperties.AuthToken,
                        taskProperties.ProjectId,
                        checkSuiteId,
                        succeeded,
                        outputLog,
                        log,
                        taskLogger,
                        variables);

                    return;
                }
                catch (Exception e)
                {
                    if (taskLogger != null)
                    {
                        await taskLogger.Log(e.ToString()).ConfigureAwait(false);
                    }

                    throw;
                }
                finally
                {
                    if (taskLogger != null)
                    {
                        await taskLogger.End().ConfigureAwait(false);
                    }
                }
            }
        }

        private static async Task UpdateCheckSuiteResult(
            string accountUrl,
            string authToken,
            Guid projectId,
            Guid checkSuiteId,
            bool succeeded,
            string message,
            ILogger log,
            TaskLogger taskLogger,
            IDictionary<string, string> variables)
        {
            string currentStatus;
            using (var httpClient = new HttpClient())
            {
                string authTokenString = string.Format("{0}:{1}", "", authToken);
                string base64AuthString = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(authTokenString));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64AuthString);

                HttpRetryHelper httpRetryHelper = new HttpRetryHelper(NumberOfHttpRetries);
                string checkSuiteResult = null;
                try
                {
                    var checkRunUrl = string.Format("{0}/{1}/_apis/pipelines/checks/runs/{2}", accountUrl, projectId, checkSuiteId);
                    Utilities.LogInformation(string.Format("Invoking {0} to get current check status", checkRunUrl), log, taskLogger, variables);
                    var getResponse = await httpRetryHelper.Invoke(async () => await httpClient.GetAsync(checkRunUrl));
                    checkSuiteResult = await getResponse.Content.ReadAsStringAsync();
                    Utilities.LogInformation($"Check suite response: {checkSuiteResult}", log, taskLogger, variables);
                }
                catch (Exception e)
                {
                    await taskLogger?.Log($"Failed to fetch current check status with error message : {e.Message}");
                    Utilities.LogInformation($"Error stack: {e.ToString()}", log, taskLogger, variables);
                }

                dynamic checkSuite = JsonConvert.DeserializeObject(checkSuiteResult);
                currentStatus = checkSuite?.status;

                if (!string.IsNullOrWhiteSpace(currentStatus)
                    && string.Compare(currentStatus, "running", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var checkSuiteUpdate = new Dictionary<string, dynamic>();
                    dynamic checkRunResult = new { status = succeeded ? "approved" : "rejected", resultMessage = message };
                    checkSuiteUpdate.Add(checkSuiteId.ToString(), checkRunResult);

                    try
                    {
                        var updatedCheckSuiteBuffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(checkSuite));
                        var updatedCheckByteContent = new ByteArrayContent(updatedCheckSuiteBuffer);

                        updatedCheckByteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                        var updateCheckRunUrl = string.Format("{0}/{1}/_apis/pipelines/checks/runs/{2}?_api-version=5.0", accountUrl, projectId, checkSuiteId);
                        Utilities.LogInformation(string.Format("Invoking {0} to post current check status", updateCheckRunUrl), log, taskLogger, variables);
                        var updateCheckSuiteResponse = await httpRetryHelper.Invoke(async () => await httpClient.PostAsync(updateCheckRunUrl, updatedCheckByteContent));
                    }
                    catch (Exception e)
                    {
                        await taskLogger?.Log($"Failed to fetch update check status with error message : {e.Message}");
                        Utilities.LogInformation($"Error stack: {e.ToString()}", log, taskLogger, variables);
                    }
                }
            }
        }
    }
}