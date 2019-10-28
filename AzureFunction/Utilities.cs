namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Models;
    using Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Request;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using WebJobsExecutionContext = WebJobs.ExecutionContext;

    public static class Utilities
    {
        private const string ImageProvenanceFileName = "ImageProvenance.json";
        private const string PolicyFileName = "Policies.rego";
        private const string OutputResultFileName = "Output.txt";

        public static IEnumerable<string> ExecutePolicyCheck(
            WebJobsExecutionContext executionContext,
            ILogger log,
            string imageProvenance,
            string policy,
            TaskLogger taskLogger,
            out string outputLog)
        {
            string folderName = string.Format("Policy-{0}", executionContext.InvocationId.ToString("N"));
            string newFolderPath = Path.Combine(executionContext.FunctionDirectory, folderName);

            log.LogInformation(string.Format("Folder created : {0}", newFolderPath));

            Directory.CreateDirectory(newFolderPath);

            string imageProvenancePath = Path.Combine(newFolderPath, ImageProvenanceFileName);
            string policyFilePath = Path.Combine(newFolderPath, PolicyFileName);

            string packageName = Regex.Match(policy, @"package\s([a-zA-Z0-9.]+)", RegexOptions.IgnoreCase).Groups[1].Value;
            log.LogInformation(string.Format("Package name : {0}", packageName));


            if (string.IsNullOrWhiteSpace(packageName))
            {
                outputLog = "No package name could be inferred from the policy. Cannot continue execution. Ensure that policy contains a package name defined";
                taskLogger?.Log(outputLog).ConfigureAwait(false);

                return new List<string> { outputLog };
            }

            string output;

            try
            {
                File.WriteAllText(imageProvenancePath, imageProvenance);
                log.LogInformation("Image provenance file created");
                File.WriteAllText(policyFilePath, policy);
                log.LogInformation("Policy content file created");

                string arguments = string.Format(
                            CultureInfo.InvariantCulture,
                            "/c \"\"{0}\\opa_windows_amd64.exe\" eval -f pretty --explain notes -i \"{1}\\{2}\" -d \"{1}\\{3}\" \"data.{4}.violations\" > \"{1}\\{5}\"\"",
                            executionContext.FunctionAppDirectory,
                            folderName,
                            ImageProvenanceFileName,
                            PolicyFileName,
                            packageName,
                            OutputResultFileName);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        WorkingDirectory = executionContext.FunctionDirectory
                    }
                };

                taskLogger?.Log("Initiating evaluation");
                process.Start();
                taskLogger?.Log("Evaluation is in progress");
                process.WaitForExit();
                taskLogger?.Log("Evaluation complete. Processing result");
                log.LogInformation("Completed executing OPA");

                output = File.ReadAllText(string.Format("{0}\\{1}", newFolderPath, OutputResultFileName));
                log.LogInformation(output);
            }
            finally
            {
                Directory.Delete(newFolderPath, true);
            }

            return Utilities.GetViolationsFromResponse(log, taskLogger, output, out outputLog);
        }

        public static IEnumerable<string> GetViolationsFromResponse(
        ILogger log,
        TaskLogger taskLogger,
        string output,
        out string outputLog)
        {
            IEnumerable<string> violations;
            int outputValueIndex = output.IndexOf("[\n");
            log.LogInformation($" outputValueIndex : {outputValueIndex}");

            if (outputValueIndex < 0)
            {
                outputValueIndex = output.IndexOf("[]");
                log.LogInformation($" outputValueIndex : {outputValueIndex}");
            }

            string outputValueString = outputValueIndex >= 0 ? output.Substring(outputValueIndex) : string.Empty; log.LogInformation(outputValueString);

            JArray outputArray = JsonConvert.DeserializeObject(outputValueString) as JArray;
            if (outputArray != null && outputArray.Count == 1)
            {
                JArray valuesArray = outputArray[0] as JArray;

                if (valuesArray != null)
                {
                    violations = valuesArray.Values<string>().ToList();
                }
                else
                {
                    violations = new List<string>();
                }
            }
            else
            {
                violations = new List<string>();
            }

            outputLog = output;
            taskLogger?.Log(outputLog).ConfigureAwait(false);

            return violations;
        }

        public static TaskProperties CreateTaskProperties(EvaluationRequest request)
        {
            var taskPropertiesDictionary = new Dictionary<string, string>();
            taskPropertiesDictionary.Add(TaskProperties.AuthTokenKey, request.AuthToken);
            taskPropertiesDictionary.Add(TaskProperties.HubNameKey, request.HubName);
            taskPropertiesDictionary.Add(TaskProperties.PlanUrlKey, request.PlanId.ToString());
            taskPropertiesDictionary.Add(TaskProperties.JobIdKey, request.JobId.ToString());
            taskPropertiesDictionary.Add(TaskProperties.PlanIdKey, request.PlanId.ToString());
            taskPropertiesDictionary.Add(TaskProperties.TimelineIdKey, request.TimelineId.ToString());

            return new TaskProperties(taskPropertiesDictionary);
        }
    }
}
