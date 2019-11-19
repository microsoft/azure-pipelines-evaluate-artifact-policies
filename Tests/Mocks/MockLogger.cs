namespace Microsoft.Azure.Pipelines.EvaluateArtifactPolicies.Test.Mocks
{
    using System;
    using System.Text;

    using Microsoft.Extensions.Logging;

    public class MockLogger : ILogger, IDisposable
    {
        private StringBuilder mockLog;

        public MockLogger()
        {
            this.mockLog = new StringBuilder();
        }

        public string Logs
        {
            get
            {
                return this.mockLog.ToString();
            }
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            this.mockLog.Clear();
            this.mockLog = null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            this.mockLog.Append(state.ToString());
        }
    }
}
