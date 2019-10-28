namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Models
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public class EvaluationRequest
    {
        [DataMember(EmitDefaultValue = false)]
        public IList<object> ImageProvenance { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string PolicyData { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string HostUrl { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid ProjectId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string AuthToken { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid PlanId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid CheckSuiteId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string HubName { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid JobId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid TimelineId { get; set; }
    }
}
