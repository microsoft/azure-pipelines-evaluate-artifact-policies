namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Models;
    using Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Request;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using WebJobsExecutionContext = WebJobs.ExecutionContext;

    public static class CommonUtilities
    {
        private const string ImageProvenanceFileName = "ImageProvenance.json";
        private const string PolicyFileName = "Policies.rego";
        private const string OutputResultFileName = "Output.txt";
        private const int PackageRegexMatchTimeout = 1; // 1 second

        private const string DebugVariableName = "system.debug";

        public static IEnumerable<string> ExecutePolicyCheck(
            WebJobsExecutionContext executionContext,
            ILogger log,
            string imageProvenance,
            string policy,
            TaskLogger taskLogger,
            IDictionary<string, string> variables,
            StringBuilder syncLogger,
            out ViolationType violationType,
            out string outputLog)
        {
            string folderName = string.Format("Policy-{0}", executionContext.InvocationId.ToString("N"));
            string newFolderPath = Path.Combine(executionContext.FunctionDirectory, folderName);

            string imageProvenancePath = Path.Combine(newFolderPath, ImageProvenanceFileName);
            string policyFilePath = Path.Combine(newFolderPath, PolicyFileName);

            string packageName = Regex.Match(policy, @"package\s([a-zA-Z0-9.]+)", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(PackageRegexMatchTimeout)).Groups?[1].Value;
            CommonUtilities.LogInformation(string.Format(CultureInfo.InvariantCulture, "Package name : {0}", packageName), log, taskLogger, variables, syncLogger);


            if (string.IsNullOrWhiteSpace(packageName))
            {
                outputLog = "No package name could be inferred from the policy. Cannot continue execution. Ensure that policy contains a package name defined";
                CommonUtilities.LogInformation(outputLog, log, taskLogger, variables, syncLogger, true);

                violationType = ViolationType.PackageNotDefined;
                return new List<string> { outputLog };
            }

            string output;

            try
            {
                Directory.CreateDirectory(newFolderPath);
                log.LogInformation(string.Format("Folder created : {0}", newFolderPath), log, taskLogger, variables, syncLogger);

                File.WriteAllText(imageProvenancePath, imageProvenance);
                string formattedImageProvenance = IsDebugEnabled(variables) ? JValue.Parse(imageProvenance).ToString(Formatting.Indented) : imageProvenance;
                CommonUtilities.LogInformation("Image provenance file created", log, taskLogger, variables, syncLogger);
                CommonUtilities.LogInformation($"Image provenance : \r\n{formattedImageProvenance}", log, taskLogger, variables, syncLogger);
                File.WriteAllText(policyFilePath, policy);
                CommonUtilities.LogInformation("Policy content file created", log, taskLogger, variables, syncLogger);
                CommonUtilities.LogInformation($"Policy definitions : \r\n{policy}", log, taskLogger, variables, syncLogger);

                string arguments = GetProcessArguments(log, taskLogger, variables, syncLogger, folderName, packageName);

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

                CommonUtilities.LogInformation("Initiating evaluation", log, taskLogger, variables, syncLogger, true);
                process.Start();
                CommonUtilities.LogInformation("Evaluation is in progress", log, taskLogger, variables, syncLogger, true);
                process.WaitForExit();
                CommonUtilities.LogInformation("Evaluation complete. Processing result", log, taskLogger, variables, syncLogger, true);
                CommonUtilities.LogInformation($"Completed executing with exit code {process.ExitCode}", log, taskLogger, variables, syncLogger);

                output = File.ReadAllText(string.Format(CultureInfo.InvariantCulture, "{0}\\{1}", newFolderPath, OutputResultFileName));
                log.LogInformation(output);

                if (process.ExitCode != 0)
                {
                    outputLog = output;
                    violationType = ViolationType.PolicyExecutionError;
                    return new List<string> { $"Policy run had issues: {output}" };
                }
            }
            finally
            {
                Directory.Delete(newFolderPath, true);
            }

            return CommonUtilities.GetViolationsFromResponse(log, taskLogger, output, variables, syncLogger, out violationType, out outputLog);
        }

        public static IEnumerable<string> GetViolationsFromResponse(
        ILogger log,
        TaskLogger taskLogger,
        string output,
        IDictionary<string, string> variables,
        StringBuilder syncLogger,
        out ViolationType violationType,
        out string outputLog)
        {
            IEnumerable<string> violations;
            int outputValueIndex = output.IndexOf("[\n");
            log.LogInformation($" outputValueIndex of [ <new line> : {outputValueIndex}");

            if (outputValueIndex < 0)
            {
                outputValueIndex = output.IndexOf("[]");
                log.LogInformation($" outputValueIndex of [] : {outputValueIndex}");
            }

            string outputValueString = outputValueIndex >= 0 ? output.Substring(outputValueIndex) : string.Empty;
            CommonUtilities.LogInformation($"Output of policy check : {outputValueString}", log, taskLogger, variables, syncLogger);

            JArray outputArray = JsonConvert.DeserializeObject(outputValueString) as JArray;
            if (outputArray != null)
            {
                violations = outputArray.Values().Select(val => val.ToString(Formatting.Indented)).ToList();
                violationType = violations.Any() ? ViolationType.ViolationsListNotEmpty : ViolationType.None;
            }
            else
            {
                if (output.IndexOf("undefined") == 0 || output.IndexOf("\nundefined") > 0)
                {
                    violationType = ViolationType.ViolationsNotDefined;
                    violations = new List<string> { "violations is not defined in the policy. Please define a rule called violations" };
                }
                else
                {
                    violationType = ViolationType.None;
                    violations = new List<string>();
                }
            }

            // Get every line in the log to a new line, for better readability in the logs pane
            // Without this step, each line of the output won't go to a new line number
            outputLog = Regex.Replace(output, @"(?<=\S)\n", "\r\n");
            CommonUtilities.LogInformation(outputLog, log, taskLogger, variables, syncLogger, true);

            return violations;
        }

        public static TaskProperties CreateTaskProperties(EvaluationRequest request)
        {
            var taskPropertiesDictionary = new Dictionary<string, string>();
            taskPropertiesDictionary.Add(TaskProperties.ProjectIdKey, request.ProjectId.ToString());
            taskPropertiesDictionary.Add(TaskProperties.AuthTokenKey, request.AuthToken);
            taskPropertiesDictionary.Add(TaskProperties.HubNameKey, request.HubName);
            taskPropertiesDictionary.Add(TaskProperties.PlanUrlKey, request.HostUrl.ToString());
            taskPropertiesDictionary.Add(TaskProperties.JobIdKey, request.JobId.ToString());
            taskPropertiesDictionary.Add(TaskProperties.PlanIdKey, request.PlanId.ToString());
            taskPropertiesDictionary.Add(TaskProperties.TimelineIdKey, request.TimelineId.ToString());

            return new TaskProperties(taskPropertiesDictionary);
        }

        public static void LogInformation(string message, ILogger log, TaskLogger taskLogger, IDictionary<string, string> variables, StringBuilder syncLogger, bool alwaysLog = false)
        {
            if (alwaysLog
                || IsDebugEnabled(variables))
            {
                taskLogger?.Log(message).ConfigureAwait(false);
                syncLogger?.AppendLine(message);
            }

            log.LogInformation(message);
        }

        public static string GetProcessArguments(ILogger log, TaskLogger taskLogger, IDictionary<string, string> variables, StringBuilder syncLogger, string folderName, string packageName)
        {
            // Add full explanation in case debug is set to true
            var explainMode = IsDebugEnabled(variables) ? "full" : "notes";

            // 2>&1 ensures standard error is directed to standard output
            string arguments = string.Format(
                        CultureInfo.InvariantCulture,
                        "/c \"\"..\\opa_windows_amd64.exe\" eval -f pretty --explain {5} -i \"{0}\\{1}\" -d \"{0}\\{2}\" \"data.{3}.violations\" > \"{0}\\{4}\" 2>&1\"",
                        folderName,
                        ImageProvenanceFileName,
                        PolicyFileName,
                        packageName,
                        OutputResultFileName,
                        explainMode);

            CommonUtilities.LogInformation($"Command line cmd: {arguments}", log, taskLogger, variables, syncLogger);
            return arguments;
        }

        public static bool IsDebugEnabled(IDictionary<string, string> variables)
        {
            if (variables == null)
            {
                return false;
            }

            string debugVariableValueString;
            bool debugVariableValue;
            return variables.TryGetValue(DebugVariableName, out debugVariableValueString)
                && bool.TryParse(debugVariableValueString, out debugVariableValue)
                && debugVariableValue;
        }
    }
}
