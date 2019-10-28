namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Request
{
    // use this in TaskProperties to determine if the request type is Execute or Cancel and call respective api
    public enum RequestType
    {
        Execute,
        Cancel
    }
}