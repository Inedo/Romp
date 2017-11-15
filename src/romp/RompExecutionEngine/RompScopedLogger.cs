using System;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;
using Inedo.Romp.Data;

namespace Inedo.Romp.RompExecutionEngine
{
    internal sealed class RompScopedLogger : ActiveNamedScope
    {
        private static readonly object LogLock = new object();
        private readonly RompExecutionEnvironment executer;
        private int scopeSequence;

        public RompScopedLogger(RompExecutionEnvironment executer, ScopeIdentifier current, ActiveNamedScope parent)
            : base(current, parent)
        {
            this.executer = executer;
        }

        public void Log(MessageLevel logLevel, string message)
        {
            lock (LogLock)
            {
                Logger.Log(logLevel, message);

                int? executionId = this.executer?.ExecutionId;
                if (executionId > 0 && this.scopeSequence > 0)
                    RompDb.WriteLogMessage((int)executionId, this.scopeSequence, (int)logLevel, message, DateTime.UtcNow);
            }
        }
        public override void Log(LogLevel level, string message) => this.Log((MessageLevel)level, message);
        public override void BeginScope()
        {
            lock (LogLock)
            {
                var parent = (RompScopedLogger)this.Parent;
                if (this.executer.LogToDatabase)
                    this.scopeSequence = RompDb.CreateLogScope((int)this.executer.ExecutionId, parent?.scopeSequence, this.Current.LocalName, DateTime.UtcNow);
            }
        }
        public override void EndScope()
        {
            lock (LogLock)
            {
                if (this.executer.LogToDatabase)
                    RompDb.CompleteLogScope((int)this.executer.ExecutionId, this.scopeSequence, DateTime.UtcNow);
            }
        }
    }
}
