namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Request;
    using Microsoft.TeamFoundation.DistributedTask.WebApi;
    using Microsoft.VisualStudio.Services.Common;
    using Microsoft.VisualStudio.Services.WebApi;

    public class TaskClient : IDisposable
    {
        private readonly TaskProperties taskProperties;
        private TaskHttpClient taskClient;
        private VssConnection vssConnection;

        public TaskClient(TaskProperties taskProperties)
        {
            this.taskProperties = taskProperties;
            var vssBasicCredential = new VssBasicCredential(string.Empty, taskProperties.AuthToken);
            vssConnection = new VssConnection(new Uri(taskProperties.PlanUrl), vssBasicCredential);
            taskClient = vssConnection.GetClient<TaskHttpClient>();
        }

        public async Task UpdateTimelineRecordsAsync(TimelineRecord timelineRecord, CancellationToken cancellationToken)
        {
            await taskClient.UpdateTimelineRecordsAsync(this.taskProperties.ProjectId, this.taskProperties.HubName, this.taskProperties.PlanId, this.taskProperties.TimelineId, new List<TimelineRecord> { timelineRecord }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<TaskLog> CreateLogAsync(TaskLog log)
        {
            return await taskClient.CreateLogAsync(this.taskProperties.ProjectId, this.taskProperties.HubName, this.taskProperties.PlanId, log).ConfigureAwait(false);
        }

        public async Task<TaskLog> AppendLogContentAsync(int logId, Stream uploadStream)
        {
            return await taskClient.AppendLogContentAsync(this.taskProperties.ProjectId, this.taskProperties.HubName, this.taskProperties.PlanId, logId, uploadStream).ConfigureAwait(false);
        }

        public async Task AppendTimelineRecordFeedAsync(IEnumerable<string> lines)
        {
            await taskClient.AppendTimelineRecordFeedAsync(this.taskProperties.ProjectId, this.taskProperties.HubName, this.taskProperties.PlanId, this.taskProperties.TimelineId, this.taskProperties.JobId, lines).ConfigureAwait(false);
        }

        public void Dispose()
        {
            vssConnection?.Dispose();
            taskClient?.Dispose();
            vssConnection = null;
            taskClient = null;
        }
    }
}