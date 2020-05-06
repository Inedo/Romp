using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Romp.Data;

namespace Inedo.Romp.RompExecutionEngine
{
    /// <summary>
    /// Wraps an execution log that is written directly to the database. See Remarks.
    /// </summary>
    /// <remarks>
    /// This logger maximizes concurrency by generating unique ascending sequence numbers
    /// itself, and not relying on locking at the database level to do this. It also issues all
    /// database calls asynchronously and returns execution to the caller immediately, maximizing
    /// execution throughput. When an execution is completed, the <see cref="CompleteScopeAsync()"/>
    /// method must be called at the root log scope to make sure everything gets written.
    /// </remarks>
    public sealed class BackgroundLogScope
    {
        private int maxLevel = -1;
        private List<LogEntry> entries = new List<LogEntry>();
        private readonly List<BackgroundLogScope> childScopes = new List<BackgroundLogScope>();
        private readonly object syncLock = new object();
        private DateTime? completed;
        private Task writeLogMessagesTask;
        private Task completeLogScopeTask;

        internal BackgroundLogScope(ExecutionLogger executionLogger, string name, Task<int?> createdInDbTask)
        {
            this.ExecutionLogger = executionLogger;
            this.ScopeId = createdInDbTask;
            this.Name = name;
        }

        /// <summary>
        /// Gets the parent <see cref="ExecutionLogger"/> instance.
        /// </summary>
        public ExecutionLogger ExecutionLogger { get; }
        /// <summary>
        /// Gets the execution ID.
        /// </summary>
        public int ExecutionId => this.ExecutionLogger.ExecutionId;
        /// <summary>
        /// Gets the scope name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// Gets the scope ID if it has been created yet.
        /// </summary>
        public Task<int?> ScopeId { get; }

        /// <summary>
        /// Posts a log message to be written to the database as soon as possible.
        /// </summary>
        /// <param name="level">The message level.</param>
        /// <param name="message">The message text.</param>
        public void WriteMessage(MessageLevel level, string message)
        {
            var now = DateTime.UtcNow;

            lock (this.syncLock)
            {
                if (this.completed.HasValue)
                    throw new InvalidOperationException($"Log scope {this.Name} has already been completed.");

                if (this.maxLevel < (int)level)
                    this.maxLevel = (int)level;

                // scope has not been created yet or waiting on messages to be written
                if (!this.ScopeId.IsCompleted || this.writeLogMessagesTask != null)
                {
                    this.entries.Add(new LogEntry(this.ExecutionLogger.GetNextLogEntrySequence(), level, message));
                    if (this.writeLogMessagesTask == null)
                        this.writeLogMessagesTask = this.ScopeId.ContinueWith(this.MessagesWritten);
                }
                // scope is created and nothing is being written
                else
                {
                    this.writeLogMessagesTask = Task.Run(() => RompDb.WriteLogMessagesAsync(
                        executionId: this.ExecutionId,
                        scopeSequence: this.ScopeId.Result.Value,
                        new[] { new LogEntry(this.ExecutionLogger.GetNextLogEntrySequence(), level, message) }
                    )).ContinueWith(this.MessagesWritten);
                }
            }
        }
        /// <summary>
        /// Completes the log scope with the current time.
        /// </summary>
        public void CompleteLogScope() => this.CompleteScopeAsync(DateTime.UtcNow);
        /// <summary>
        /// Completes the log scope with the specified time.
        /// </summary>
        /// <param name="endTime">Log scope end time.</param>
        public Task CompleteScopeAsync(DateTime endTime)
        {
            lock (this.syncLock)
            {
                if (this.completeLogScopeTask != null)
                    return this.completeLogScopeTask;

                this.completed = endTime;
                this.completeLogScopeTask = innerCompleteAsync();
            }

            return innerCompleteAsync();

            async Task innerCompleteAsync()
            {
                var tasks = new List<Task>(this.childScopes.Count + 1);
                var writeTask = this.writeLogMessagesTask;
                if (writeTask != null)
                    tasks.Add(writeTask);

                if (this.childScopes.Count > 0)
                {
                    foreach (var s in this.childScopes)
                        tasks.Add(s.CompleteScopeAsync(endTime));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (var s in this.childScopes)
                {
                    if (s.maxLevel > this.maxLevel)
                        this.maxLevel = s.maxLevel;
                }

                await RompDb.CompleteLogScopeAsync(this.ExecutionId, this.ScopeId.Result.Value, endTime);
            }
        }
        /// <summary>
        /// Creates a child log scope.
        /// </summary>
        /// <param name="scopeName">The child scope name.</param>
        /// <returns>Newly-created scope.</returns>
        public BackgroundLogScope CreateChildScope(string scopeName)
        {
            var now = DateTime.UtcNow;
            int scopeSequence;

            lock (this.syncLock)
            {
                if (this.completed.HasValue)
                    throw new InvalidOperationException($"Log scope \"{this.Name}\" has alrady been completed.");

                scopeSequence = this.ExecutionLogger.GetNextScopeSequence();
                var scope = new BackgroundLogScope(this.ExecutionLogger, scopeName ?? string.Empty, Task.Run(createScopeInternalAsync));
                this.childScopes.Add(scope);
                return scope;
            }

            async Task<int?> createScopeInternalAsync()
            {
                int? parentScopeId = await this.ScopeId.ConfigureAwait(false);
                await RompDb.CreateLogScopeAsync(this.ExecutionId, scopeSequence, parentScopeId, scopeName ?? string.Empty, now);
                return scopeSequence;
            }
        }

        private void MessagesWritten(Task task)
        {
            lock (this.syncLock)
            {
                if (this.entries.Count > 0)
                {
                    var entries = this.entries;
                    var writeTask = Task.Run(() => RompDb.WriteLogMessagesAsync(this.ExecutionId, (int)this.ScopeId.Result, entries));

                    this.writeLogMessagesTask = writeTask.ContinueWith(this.MessagesWritten);
                    this.entries = new List<LogEntry>();
                }
                else
                {
                    this.writeLogMessagesTask = null;
                }
            }
        }

        private sealed class LogEntry : IExecutionLogMessage
        {
            public LogEntry(int sequence, MessageLevel level, string message)
            {
                this.Sequence = sequence;
                this.Level = level;
                this.Message = message;
            }

            public int Sequence { get; }
            public MessageLevel Level { get; }
            public string Message { get; }
            public DateTime DateTime { get; } = DateTime.UtcNow;
        }
    }
}
