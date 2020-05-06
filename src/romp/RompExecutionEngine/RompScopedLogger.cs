using System.Collections.Generic;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;

namespace Inedo.Romp.RompExecutionEngine
{
    internal sealed class RompScopedLogger : ActiveNamedScope, ILogSink
    {
        private readonly RompExecutionEnvironment executer;
        private IEnumerable<IMessage> initialMessages;

        public RompScopedLogger(RompExecutionEnvironment executer, ScopeIdentifier current, ActiveNamedScope parent, IEnumerable<IMessage> initialMessages = null)
            : base(current, parent)
        {
            this.executer = executer;
            this.initialMessages = initialMessages;
        }

        public RompScopedExecutionLog Scope { get; private set; }

        public void Log(IMessage message)
        {
            Logger.Log(message);
            this.Scope?.Log(message);
        }
        public override void Log(LogLevel level, string message) => this.Log((MessageLevel)level, message);
        public override void BeginScope()
        {
            var parent = (RompScopedLogger)this.Parent;

            if (parent != null)
                this.Scope = parent.Scope.CreateChildScope(this.Current.LocalName);
            else
                this.Scope = this.executer.RootExecutionLog.CreateChildScope(this.Current.LocalName);

            if (this.initialMessages != null)
            {
                this.Scope.Log(this.initialMessages);
                this.initialMessages = null;
            }
        }
        public override void EndScope()
        {
            if (this.Parent != null)
            {
                this.Scope?.Dispose();
                this.Scope = null;
            }
        }
    }
}
