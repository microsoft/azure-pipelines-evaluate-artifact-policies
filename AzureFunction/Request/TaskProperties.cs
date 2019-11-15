namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Request
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class TaskProperties
    {
        public Guid ProjectId { get; }
        public string HubName { get; }
        public Guid PlanId { get; }
        public string PlanUrl { get; }
        public Guid JobId { get; }
        public Guid TimelineId { get; }
        public Guid TaskInstanceId { get; set; }
        public string TaskInstanceName { get; }
        public string AuthToken { get; }
        public RequestType RequestType { get; }

        private static readonly List<string> MandatoryProperties = new List<string>
        {
            ProjectIdKey,
            PlanIdKey,
            PlanUrlKey,
            JobIdKey,
            TimelineIdKey,
            AuthTokenKey,
        };

        public static readonly List<string> PropertiesList = new List<string>(MandatoryProperties)
        {
            HubNameKey,
            TaskInstanceIdKey,
            TaskInstanceNameKey,
            RequestTypeKey,
        };

        public TaskProperties(IDictionary<string, string> messageProperties)
        {
            ValidateProperties(messageProperties);

            this.ProjectId = ParseGuid(messageProperties, ProjectIdKey);
            this.JobId = ParseGuid(messageProperties, JobIdKey);
            this.PlanId = ParseGuid(messageProperties, PlanIdKey);
            this.TimelineId = ParseGuid(messageProperties, TimelineIdKey);

            this.TaskInstanceId = messageProperties.ContainsKey(TaskInstanceIdKey)
                ? ParseGuid(messageProperties, TaskInstanceIdKey)
                : Guid.Empty;

            this.HubName = messageProperties.ContainsKey(HubNameKey) ? messageProperties[HubNameKey] : "Build";
            if (!validHubNameList.Contains(this.HubName, StringComparer.OrdinalIgnoreCase))
            {
                var exceptionMessage = $"Invalid hub name '{this.HubName}'. Please provide valid hub name from '{string.Join(", ", validHubNameList)}'.";
                throw new InvalidDataException(exceptionMessage);
            }

            var planUrl = messageProperties[PlanUrlKey];
            this.PlanUrl = planUrl;

            this.AuthToken = messageProperties[AuthTokenKey];

            var requestTypeString = messageProperties.ContainsKey(RequestTypeKey) ? messageProperties[RequestTypeKey] : RequestType.Execute.ToString();
            if (Enum.TryParse<RequestType>(requestTypeString, out var requestType))
            {
                this.RequestType = requestType;
            }

            if (messageProperties.ContainsKey(TaskInstanceNameKey))
            {
                this.TaskInstanceName = messageProperties[TaskInstanceNameKey];
            }
        }

        private static void ValidateProperties(IDictionary<string, string> messageProperties)
        {
            if (messageProperties == null)
            {
                throw new ArgumentNullException(nameof(messageProperties));
            }

            var missingProperties = MandatoryProperties
                .Where(propertyToCheck => !messageProperties.ContainsKey(propertyToCheck)).ToList();
            if (missingProperties.Any())
            {
                var exceptionMessage =
                    $"Required properties '{string.Join(", ", missingProperties)}' are missing. Please provide these values and try again.";
                throw new InvalidDataException(exceptionMessage);
            }
        }

        private static Guid ParseGuid(IDictionary<string, string> messageProperties, string propertyName)
        {
            var messageProperty = messageProperties[propertyName];
            if (!Guid.TryParse(messageProperty, out var propertyGuidValue))
            {
                throw new InvalidDataException($"Invalid guid value '{messageProperty}' provided for {propertyName}");
            }

            return propertyGuidValue;
        }

        private readonly List<string> validHubNameList = new List<string> { "Build", "Gates" };
        public const string ProjectIdKey = "ProjectId";
        public const string JobIdKey = "JobId";
        public const string PlanIdKey = "PlanId";
        public const string TimelineIdKey = "TimelineId";
        public const string TaskInstanceIdKey = "TaskInstanceId";
        public const string HubNameKey = "HubName";
        public const string PlanUrlKey = "PlanUrl";
        public const string AuthTokenKey = "AuthToken";
        public const string RequestTypeKey = "RequestType";
        public const string TaskInstanceNameKey = "TaskInstanceName";
    }
}