using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Gibraltar.Agent.Metrics;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;
using Inedo.ExecutionEngine.Parser;
using Inedo.ExecutionEngine.Variables;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.RaftRepositories;
using Inedo.IO;
using Inedo.Romp.Agents;
using Inedo.Romp.Configuration;
using Inedo.Romp.Data;
using Inedo.Romp.RompPack;

namespace Inedo.Romp.RompExecutionEngine
{
    internal sealed class RompExecutionEnvironment : IExecutionHostEnvironment
    {
        private ScopedStatementBlock plan;
        private readonly Lazy<RompScopedExecutionLog> rootExecutionLogLazy;

        public RompExecutionEnvironment(ScopedStatementBlock script, bool simulate)
        {
            this.plan = script;
            this.Simulation = simulate;
            this.rootExecutionLogLazy = new Lazy<RompScopedExecutionLog>(() => RompScopedExecutionLog.Create(this.ExecutionId.GetValueOrDefault()));
        }

        public RompExecutionContext DefaultExternalContext { get; private set; }
        public LocalAgent LocalAgent { get; } = new LocalAgent();
        public bool Simulation { get; }
        public int? ExecutionId { get; private set; }
        public bool LogToDatabase => true;
        public RompScopedExecutionLog RootExecutionLog => this.rootExecutionLogLazy.Value;

        private ExecuterThread Executer { get; set; }

        object IExecutionHostEnvironment.DefaultExternalContext => this.DefaultExternalContext;

        public async Task ExecuteAsync()
        {
            var startTime = DateTime.UtcNow;
            this.ExecutionId = RompDb.CreateExecution(startTime, Domains.ExecutionStatus.Normal, Domains.ExecutionRunState.Executing, this.Simulation);
            this.DefaultExternalContext = new RompExecutionContext(this);

            this.Executer = new ExecuterThread(new AnonymousBlockStatement(this.plan), this);

            var result = ExecutionStatus.Error;
            try
            {
                await RompSessionVariable.ExpandValuesAsync(this.DefaultExternalContext);

                var targetDir = RompSessionVariable.GetSessionVariable(new RuntimeVariableName("TargetDirectory", RuntimeValueType.Scalar))?.GetValue().AsString();
                if (string.IsNullOrWhiteSpace(targetDir) || !PathEx.IsPathRooted(targetDir))
                {
                    this.Executer.RootLogScope.Log(LogLevel.Error, "Invalid value for $TargetDirectory.");
                    result = ExecutionStatus.Error;
                }
                else
                {
                    PackageInstaller.TargetDirectory = targetDir;
                    DirectoryEx.Create(targetDir);
                    result = await this.Executer.ExecuteAsync();
                }
            }
            catch (Exception ex)
            {
                if (!(ex is ExecutionFailureException))
                    Logger.Log(MessageLevel.Error, "Unhandled exception in executer: " + ex.ToString());

                result = ExecutionStatus.Error;
            }
            finally
            {
                var duration = DateTime.UtcNow.Subtract(startTime);
                try
                {
                    this.CleanTempDirectory();
                }
                catch
                {
                }

                RompDb.CompleteExecution(
                    executionId: this.ExecutionId.Value,
                    endDate: DateTime.UtcNow,
                    statusCode: result >= ExecutionStatus.Error ? Domains.ExecutionStatus.Error : result >= ExecutionStatus.Warning ? Domains.ExecutionStatus.Warning : Domains.ExecutionStatus.Normal
                );
            }
        }
        public void Cancel() => this.Executer?.Cancel();
        public ExecuterThreadStatus GetStatus() => this.Executer?.GetDetailedStatus();
        public ActiveNamedScope CreateLogScope(ScopeIdentifier current, ActiveNamedScope parent) => new RompScopedLogger(this, current, parent);
        public async Task<ActionExecutionResult> ExecuteActionAsync(ActionStatement actionStatement, IExecuterContext context)
        {
            var metric = new OtterScriptOperationEventMetric(actionStatement.ActionName.FullName);
            try
            {
                var rompContext = ((RompExecutionContext)context.ExternalContext).WithExecuterContext(context);

                var operationType = ExtensionsManager.TryGetOperation(actionStatement.ActionName.Namespace, actionStatement.ActionName.Name);
                if (operationType == null)
                {
                    context.LogScope.Log(LogLevel.Error, $"Unable to resolve operation \"{actionStatement.ActionName}\". A Hedgehog extension may be missing.");
                    return ExecutionStatus.Fault;
                }

                var operation = (Operation)Activator.CreateInstance(operationType);
                await ScriptPropertyMapper.SetPropertiesAsync(operation, actionStatement, rompContext, context);

                var loggedStatus = ExecutionStatus.Normal;
                var logScopeName = Operation.GetLogScopeName(operationType, actionStatement);
                var scopedLogger = new RompScopedLogger(this, context.LogScope.Current.Push(logScopeName), context.LogScope);
                scopedLogger.BeginScope();

                operation.MessageLogged +=
                    (s, e) =>
                    {
                        if (e.Level >= MessageLevel.Warning && loggedStatus < ExecutionStatus.Error)
                        {
                            if (e.Level == MessageLevel.Error)
                                loggedStatus = ExecutionStatus.Error;
                            else if (e.Level == MessageLevel.Warning)
                                loggedStatus = ExecutionStatus.Warning;
                        }

                        scopedLogger.Log(e.Level, e.Message);
                    };

                try
                {
                    var asyncOperation = operation as IExecutingOperation ?? throw new InvalidOperationException();

                    context.SetReportProgressDelegate(() => operation.GetProgress()?.StatementProgress ?? default);
                    await asyncOperation.ExecuteAsync(rompContext).ConfigureAwait(false);

                    ScriptPropertyMapper.ReadOutputs(operation, actionStatement.OutArguments, context);

                    return loggedStatus;
                }
                catch (ExecutionFailureException ex)
                {
                    if (!string.IsNullOrEmpty(ex.Message))
                        operation.LogError(ex.Message);

                    throw new ExecutionFailureException(ex.Message);
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException)
                    {
                        operation.LogError("Operation canceled or timeout expired.");
                        return ExecutionStatus.Fault;
                    }

                    metric.Error = ex;
                    operation.LogError("Unhandled exception: " + ex.ToString());
                    return ExecutionStatus.Error;
                }
                finally
                {
                    scopedLogger.EndScope();
                }
            }
            finally
            {
                if (RompConfig.CeipEnabled)
                    EventMetric.Write(metric);
            }
        }
        public Task<object> GetExternalContextAsync(string contextType, string contextValue, IExecuterContext currentContext)
        {
            if (string.Equals(contextType, "server", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(contextValue, "localhost", StringComparison.OrdinalIgnoreCase) || string.Equals(contextValue, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(currentContext.ExternalContext);
                else
                    throw new ExecutionFailureException("Romp server context can only be set to \"localhost\".");
            }
            else if (string.Equals(contextType, "directory", StringComparison.OrdinalIgnoreCase))
            {
                var context = (RompExecutionContext)currentContext.ExternalContext;
                return Task.FromResult<object>(context.WithDirectory(contextValue));
            }
            else
            {
                throw new ExecutionFailureException("Invalid context: " + contextType);
            }
        }
        public IVariableEvaluationContext GetVariableEvaluationContext(IExecuterContext context)
        {
            var rompContext = (RompExecutionContext)context.ExternalContext;
            return new RompVariableEvaluationContext(rompContext.WithExecuterContext(context), context);
        }
        public async Task<NamedTemplate> TryGetGlobalTemplateAsync(string templateName)
        {
            var name = QualifiedName.Parse(templateName);

            using (var raft = Factory.CreateRaftRepository(name.Namespace ?? RaftRepository.DefaultName, OpenRaftOptions.ReadOnly | OpenRaftOptions.OptimizeLoadTime))
            {
                if (raft != null)
                {
                    var rubbish = await raft.GetRaftItemsAsync();

                    var template = await raft.GetRaftItemAsync(RaftItemType.Module, name.Name + ".otter");
                    if (template != null)
                    {
                        using (var stream = await raft.OpenRaftItemAsync(RaftItemType.Module, template.ItemName, FileMode.Open, FileAccess.Read))
                        {
                            var results = Compiler.Compile(stream);
                            if (results.Script != null)
                                return results.Script.Templates.Values.FirstOrDefault();
                            else
                                throw new ExecutionFailureException($"Error processing template {name}: {string.Join(Environment.NewLine, results.Errors)}");
                        }
                    }
                }
            }

            return null;
        }

        public Task<IRuntimeVariable> TryGetGlobalVariableAsync(RuntimeVariableName variableName, IExecuterContext context) => Task.FromResult<IRuntimeVariable>(RompSessionVariable.GetSessionVariable(variableName));

        private void CleanTempDirectory()
        {
            var tempDirectory = PathEx.Combine(RompConfig.DefaultWorkingDirectory, "_E" + this.ExecutionId);
            DirectoryEx.Delete(tempDirectory);
        }

        Task<NamedTemplate> IExecutionHostEnvironment.TryGetGlobalTemplateAsync(string templateName, IExecuterContext context) => this.TryGetGlobalTemplateAsync(templateName);

        [EventMetric("Inedo", "Plans", "Operation")]
        private sealed class OtterScriptOperationEventMetric
        {
            private readonly Stopwatch stopwatch = Stopwatch.StartNew();

            public OtterScriptOperationEventMetric(string name) => this.Name = name;

            [EventMetricValue("Name", SummaryFunction.Count, null)]
            public string Name { get; }
            [EventMetricValue("StartTime", SummaryFunction.Count, null, Caption = "Start Time")]
            public DateTimeOffset StartTime { get; } = DateTimeOffset.Now;
            [EventMetricValue("EndTime", SummaryFunction.Count, null, Caption = "End Time")]
            public DateTimeOffset EndTime => this.StartTime + this.Duration;
            [EventMetricValue("Duration", SummaryFunction.Average, "ms")]
            public TimeSpan Duration => this.stopwatch.Elapsed;
            [EventMetricValue("Error", SummaryFunction.Count, null)]
            public Exception Error { get; set; }
        }
    }
}
