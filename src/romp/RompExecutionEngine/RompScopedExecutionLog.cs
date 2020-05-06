using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inedo.Diagnostics;

namespace Inedo.Romp.RompExecutionEngine
{
    internal sealed class RompScopedExecutionLog
    {
        private readonly BackgroundLogScope scope;
        private bool disposed;

        public RompScopedExecutionLog(BackgroundLogScope scope)
        {
            this.scope = scope;
        }

        public void Log(IMessage message)
        {
            if (this.disposed)
                return;

            this.scope.WriteMessage(message.Level, message.Message);
        }
        public void Log(IEnumerable<IMessage> messages)
        {
            if (this.disposed || messages == null)
                return;

            foreach (var m in messages)
                this.Log(m);
        }
        public RompScopedExecutionLog CreateChildScope(string name)
        {
            if (this.disposed)
                throw new ObjectDisposedException(nameof(RompScopedExecutionLog));

            return new RompScopedExecutionLog(this.scope.CreateChildScope(name));
        }
        public Task CompleteAllAsync() => this.scope.CompleteScopeAsync(DateTime.UtcNow);

        public static RompScopedExecutionLog Create(int executionId)
        {
            var logger = new ExecutionLogger(executionId);
            return new RompScopedExecutionLog(logger.RootLogScope);
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.disposed = true;
                this.scope.CompleteLogScope();
            }
        }
    }
}
