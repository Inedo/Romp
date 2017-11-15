using System;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;
using Inedo.ExecutionEngine.Variables;
using Inedo.Extensibility.Operations;
using Inedo.IO;
using Inedo.Romp.Agents;
using Inedo.Romp.Configuration;
using Inedo.Romp.Data;

namespace Inedo.Romp.RompExecutionEngine
{
    internal sealed class RompExecutionEnvironment : IExecutionHostEnvironment //OtterPlanExecuterBase
    {
        private ScopedStatementBlock plan;

        public RompExecutionEnvironment(ScopedStatementBlock script, bool simulate)
        {
            this.plan = script;
            this.Simulation = simulate;
        }

        public object DefaultExternalContext => throw new NotImplementedException();
        public LocalAgent LocalAgent { get; }
        public bool Simulation { get; }
        public int? ExecutionId { get; private set; }
        public bool LogToDatabase => true;

        private ExecuterThread Executer { get; set; }

        public async Task ExecuteAsync()
        {
            this.Executer = new ExecuterThread(new AnonymousBlockStatement(this.plan), this);

            var result = ExecutionStatus.Error;
            var startTime = DateTime.UtcNow;
            this.ExecutionId = RompDb.CreateExecution(startTime, Domains.ExecutionStatus.Normal, Domains.ExecutionRunState.Executing, this.Simulation);
            try
            {
                result = await this.Executer.ExecuteAsync();
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
            var rompContext = ((RompExecutionContext)context.ExternalContext).WithExecuterContext(context);

            var operationType = ExtensionsManager.TryGetOperation(actionStatement.ActionName.Namespace, actionStatement.ActionName.Name);
            if (operationType == null)
            {
                context.LogScope.Log(LogLevel.Error, $"Unable to resolve operation \"{actionStatement.ActionName}\". A Hedgehog extension may be missing.");
                return ExecutionStatus.Fault;
            }

            var operation = (Operation)Activator.CreateInstance(operationType);
            ScriptPropertyMapper.SetProperties(operation, actionStatement, rompContext, context);

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

                operation.LogError("Unhandled exception: " + ex.ToString());
                return ExecutionStatus.Error;
            }
            finally
            {
                scopedLogger.EndScope();
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
        public NamedTemplate TryGetGlobalTemplate(string templateName) => RompRaftFactory.GetTemplate(templateName);
        public IRuntimeVariable TryGetGlobalVariable(RuntimeVariableName variableName, IExecuterContext context) => RompSessionVariable.GetSessionVariable(variableName);

        private void CleanTempDirectory()
        {
            var tempDirectory = PathEx.Combine(RompConfig.DefaultWorkingDirectory, "_E" + this.ExecutionId);
            DirectoryEx.Delete(tempDirectory);
        }
    }
}
