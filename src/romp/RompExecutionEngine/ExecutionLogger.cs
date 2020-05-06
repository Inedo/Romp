using System;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Romp.Data;

namespace Inedo.Romp.RompExecutionEngine
{
    /// <summary>
    /// Manages global state for an execution log. Most of the logic is handled by the <see cref="BackgroundLogScope"/> class.
    /// </summary>
    public sealed class ExecutionLogger
    {
        private int currentLogEntrySequence;
        private int currentScopeSequence;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionLogger"/> class.
        /// </summary>
        /// <param name="executionId">The execution ID. This execution must not have any log messages or scopes written to it yet.</param>
        public ExecutionLogger(int executionId)
        {
            this.ExecutionId = executionId;
            this.RootLogScope = new BackgroundLogScope(this, string.Empty, this.CreateRootScopeAsync(this.GetNextScopeSequence()));
        }

        /// <summary>
        /// Gets the execution ID.
        /// </summary>
        public int ExecutionId { get; }
        /// <summary>
        /// Gets the root log scope.
        /// </summary>
        public BackgroundLogScope RootLogScope { get; }

        internal int GetNextLogEntrySequence() => Interlocked.Increment(ref this.currentLogEntrySequence);
        internal int GetNextScopeSequence() => Interlocked.Increment(ref this.currentScopeSequence);

        private async Task<int?> CreateRootScopeAsync(int sequence)
        {
            await Task.Run(() => RompDb.CreateLogScopeAsync(this.ExecutionId, sequence, null, string.Empty, DateTime.UtcNow));
            return sequence;
        }
    }
}
