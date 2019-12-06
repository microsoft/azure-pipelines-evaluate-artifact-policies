namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Models
{
    using System.Runtime.Serialization;

    [DataContract]
    public enum ViolationType
    {
        None,
        ViolationsListNotEmpty,
        ViolationsNotDefined,
        PackageNotDefined,
        PolicyExecutionError
    }
}
