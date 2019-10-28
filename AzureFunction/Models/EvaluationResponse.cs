using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Models
{
    [DataContract]
    public class EvaluationResponse
    {
        [DataMember(Name = "violations")]
        public IList<string> Violations { get; set; }

        [DataMember(Name = "logs")]
        public string Logs { get; set; }
    }
}
