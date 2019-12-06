namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Models
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public class EvaluationResponse
    {
        [DataMember(Name = "violations")]
        public IList<string> Violations { get; set; }

        [DataMember(Name = "logs")]
        public string Logs { get; set; }

        [DataMember(Name = "violationType")]
        [JsonConverter(typeof(StringEnumConverter), true)]
        public ViolationType ViolationType { get; set; }
    }
}
