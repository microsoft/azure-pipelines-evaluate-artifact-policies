namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Models;
    using Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Test.Mocks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UtilitiesTests
    {
        [TestInitialize]
        public void Initialize()
        {
            this.mockLogger = new MockLogger();
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.mockLogger.Dispose();
        }

        [TestMethod]
        public void IsDebugEnabledShouldReturnCorrectly()
        {
            var variables = new Dictionary<string, string>();
            Assert.IsFalse(Utilities.IsDebugEnabled(null), "False for null variables");
            Assert.IsFalse(Utilities.IsDebugEnabled(variables), "False for variables without system.debug");

            variables["system.debug"] = "false";
            Assert.IsFalse(Utilities.IsDebugEnabled(variables), "False for variables with system.debug set to false");
            variables["system.debug"] = string.Empty;
            Assert.IsFalse(Utilities.IsDebugEnabled(variables), "False for variables with system.debug set to empty");
            variables["system.debug"] = "true";
            Assert.IsTrue(Utilities.IsDebugEnabled(variables), "True for variables with system.debug set to true");
        }

        [TestMethod]
        public void GetProcessArgumentsShouldReturnCorrectly()
        {
            string folderName = "Folder1";
            string packageName = "testPackage";
            string expectedOutputNotes = "/c \"\"..\\opa_windows_amd64.exe\" eval -f pretty --explain notes -i \"Folder1\\ImageProvenance.json\" -d \"Folder1\\Policies.rego\" \"data.testPackage.violations\" > \"Folder1\\Output.txt\" 2>&1\"";
            string expectedOutputFull = "/c \"\"..\\opa_windows_amd64.exe\" eval -f pretty --explain full -i \"Folder1\\ImageProvenance.json\" -d \"Folder1\\Policies.rego\" \"data.testPackage.violations\" > \"Folder1\\Output.txt\" 2>&1\"";

            var variables = new Dictionary<string, string>();
            variables["system.debug"] = "false";
            Assert.AreEqual(expectedOutputNotes, Utilities.GetProcessArguments(this.mockLogger, null, variables, null, folderName, packageName));

            variables["system.debug"] = "true";
            Assert.AreEqual(expectedOutputFull, Utilities.GetProcessArguments(this.mockLogger, null, variables, null, folderName, packageName));
        }

        [TestMethod]
        public void CreateTaskPropertiesShouldCreateCorrectObject()
        {
            EvaluationRequest request = new EvaluationRequest
            {
                ProjectId = Guid.NewGuid(),
                PlanId = Guid.NewGuid(),
                HostUrl = "hostUrl1",
                JobId = Guid.NewGuid(),
                HubName = "Build",
                TimelineId = Guid.NewGuid(),
                AuthToken = "authToken1"
            };

            var taskProperties = Utilities.CreateTaskProperties(request);
            Assert.AreEqual(request.ProjectId, taskProperties.ProjectId, "Project id should match");
            Assert.AreEqual(request.PlanId, taskProperties.PlanId, "Plan id should match");
            Assert.AreEqual(request.JobId, taskProperties.JobId, "Job id should match");
            Assert.AreEqual(request.HubName, taskProperties.HubName, "Hub name should match");
            Assert.AreEqual(request.AuthToken, taskProperties.AuthToken, "Auth token should match");
            Assert.AreEqual(request.HostUrl, taskProperties.PlanUrl, "Plan url should match");
            Assert.AreEqual(request.TimelineId, taskProperties.TimelineId, "Timeline id should match");
        }

        [TestMethod]
        public void LogInformationShouldLogCorrectlyForDebugDisabled()
        {
            var syncLogger = new StringBuilder();
            string message = "Sample message";
            var variables = new Dictionary<string, string>();
            variables["system.debug"] = "false";
            Utilities.LogInformation(message, this.mockLogger, null, variables, syncLogger);

            Assert.AreEqual(message, this.mockLogger.Logs, "Message should be logged in trace for system.debug false");
            Assert.AreEqual(string.Empty, syncLogger.ToString(), "Message should not be logged in task logs for system.debug false");
        }

        [TestMethod]
        public void LogInformationShouldLogCorrectlyForDebugEnabled()
        {
            var syncLogger = new StringBuilder();
            string message = "Sample message";
            var variables = new Dictionary<string, string>();

            variables["system.debug"] = "true";
            Utilities.LogInformation(message, this.mockLogger, null, variables, syncLogger);

            Assert.AreEqual(message, this.mockLogger.Logs, "Message should be logged in trace for system.debug true");
            Assert.AreEqual(message + "\r\n", syncLogger.ToString(), "Message should be logged in task logs for system.debug true");
        }

        [TestMethod]
        public void LogInformationShouldLogCorrectlyForAlwaysLog()
        {
            var syncLogger = new StringBuilder();
            string message = "Sample message";
            var variables = new Dictionary<string, string>();
            variables["system.debug"] = "false";
            Utilities.LogInformation(message, this.mockLogger, null, variables, syncLogger, true);

            Assert.AreEqual(message, this.mockLogger.Logs, "Message should be logged in trace for system.debug false, always log true");
            Assert.AreEqual(message + "\r\n", syncLogger.ToString(), "Message should  be logged in task logs for system.debug false, always log true");
        }

        [TestMethod]
        public void GetViolationsFromResponseShouldReturnCorrectlyForNoNotes()
        {
            string outputLog = null;
            var variables = new Dictionary<string, string>();
            variables["system.debug"] = "false";
            string output = "[\n   \"Failure message\"\n]";
            var violations = Utilities.GetViolationsFromResponse(this.mockLogger, null, output, variables, null, out outputLog);

            Assert.AreEqual("[\r\n   \"Failure message\"\r\n]", outputLog);
            Assert.AreEqual(1, violations.Count());
            Assert.AreEqual("\"Failure message\"", violations.ElementAt(0));
        }

        [TestMethod]
        public void GetViolationsFromResponseShouldReturnCorrectlyForUndefinedResult()
        {
            string outputLog = null;
            var variables = new Dictionary<string, string>();
            variables["system.debug"] = "false";
            string output = "undefined";
            var violations = Utilities.GetViolationsFromResponse(this.mockLogger, null, output, variables, null, out outputLog);

            Assert.AreEqual("undefined", outputLog);
            Assert.AreEqual(1, violations.Count());
            Assert.AreEqual("violations is not defined in the policy. Please define a rule called violations", violations.ElementAt(0));

            output = "[Enter data.test.violations = _\n| Enter function1\n|| Enter function2\n||| Note \"trace 01\"]\nundefined";
            violations = Utilities.GetViolationsFromResponse(this.mockLogger, null, output, variables, null, out outputLog);

            Assert.AreEqual("[Enter data.test.violations = _\r\n| Enter function1\r\n|| Enter function2\r\n||| Note \"trace 01\"]\r\nundefined", outputLog);
            Assert.AreEqual(1, violations.Count());
            Assert.AreEqual("violations is not defined in the policy. Please define a rule called violations", violations.ElementAt(0));
        }

        [TestMethod]
        public void GetViolationsFromResponseShouldReturnCorrectlyForEmptyArray()
        {
            string outputLog = null;
            var variables = new Dictionary<string, string>();
            variables["system.debug"] = "false";
            string output = "[]";
            var violations = Utilities.GetViolationsFromResponse(this.mockLogger, null, output, variables, null, out outputLog);

            Assert.AreEqual("[]", outputLog);
            Assert.AreEqual(0, violations.Count());

            output = "[Enter data.test.violations = _\n| Enter function1\n|| Enter function2\n||| Note \"trace 01\"]\n[]";
            violations = Utilities.GetViolationsFromResponse(this.mockLogger, null, output, variables, null, out outputLog);

            Assert.AreEqual("[Enter data.test.violations = _\r\n| Enter function1\r\n|| Enter function2\r\n||| Note \"trace 01\"]\r\n[]", outputLog);
            Assert.AreEqual(0, violations.Count());
        }

        [TestMethod]
        public void GetViolationsFromResponseShouldReturnCorrectlyForNotesAndViolations()
        {
            string outputLog = null;
            var variables = new Dictionary<string, string>();
            variables["system.debug"] = "false";

            string output = "[Enter data.test.violations = _\n| Enter function1\n|| Enter function2\n||| Note \"trace 01\"]\n[\n  \"Failure message\"\n]";
            var violations = Utilities.GetViolationsFromResponse(this.mockLogger, null, output, variables, null, out outputLog);

            Assert.AreEqual("[Enter data.test.violations = _\r\n| Enter function1\r\n|| Enter function2\r\n||| Note \"trace 01\"]\r\n[\r\n  \"Failure message\"\r\n]", outputLog);
            Assert.AreEqual(1, violations.Count());
            Assert.AreEqual("\"Failure message\"", violations.ElementAt(0));
        }

        private MockLogger mockLogger;
    }
}
