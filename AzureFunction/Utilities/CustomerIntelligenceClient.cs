namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.Services.Common;
    using Microsoft.VisualStudio.Services.CustomerIntelligence.WebApi;
    using Microsoft.VisualStudio.Services.WebApi;
    using Microsoft.VisualStudio.Services.WebPlatform;

    public class CustomerIntelligenceClient
    {
        private CustomerIntelligenceClient(string url, string authToken)
        {
            var vssBasicCredential = new VssBasicCredential(string.Empty, authToken);
            var vssConnection = new VssConnection(new Uri(url), vssBasicCredential);
            this.customerIntelligenceHttpClient = vssConnection.GetClient<CustomerIntelligenceHttpClient>();
        }

        public static CustomerIntelligenceClient GetClient(string url, string authToken)
        {
            if (singleton == null)
            {
                singleton = new CustomerIntelligenceClient(url, authToken);
            }

            return singleton;
        }

        public async Task PublishArtifactPolicyEventAsync(Dictionary<string, object> properties)
        {
            CustomerIntelligenceEvent ciEvent = new CustomerIntelligenceEvent
            {
                Area = ArtifactPolicyTelemetryArea,
                Feature = ArtifactPolicyEvaluateFeature,
                Properties = properties
            };

            await this.customerIntelligenceHttpClient.PublishEventsAsync(new CustomerIntelligenceEvent[] { ciEvent });

        }

        private CustomerIntelligenceHttpClient customerIntelligenceHttpClient;

        private static CustomerIntelligenceClient singleton;
        private const string ArtifactPolicyTelemetryArea = "ArtifactPolicy";
        private const string ArtifactPolicyEvaluateFeature = "ArtifactPolicyEvaluate";
    }
}
