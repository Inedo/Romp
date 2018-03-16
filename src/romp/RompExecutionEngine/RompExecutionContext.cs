using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;
using Inedo.ExecutionEngine.Templating;
using Inedo.ExecutionEngine.Variables;
using Inedo.Extensibility.Agents;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;
using Inedo.IO;
using Inedo.Romp.RompPack;

namespace Inedo.Romp.RompExecutionEngine
{
    internal sealed class RompExecutionContext : IOperationExecutionContext, IVariableFunctionContext
    {
        private RompExecutionEnvironment environment;
        private string workingDirectoryOverride;

        public RompExecutionContext(RompExecutionEnvironment environment) => this.environment = environment;
        private RompExecutionContext(RompExecutionContext source)
        {
            this.environment = source.environment;
            this.workingDirectoryOverride = source.workingDirectoryOverride;
            this.ExecuterContext = source.ExecuterContext;
        }

        public int ExecutionId => this.environment.ExecutionId ?? 0;
        public string WorkingDirectory => this.DetermineWorkingDirectory();
        public IExecuterContext ExecuterContext { get; private set; }

        public Agent Agent => this.environment.LocalAgent;
        public string ServerName => "localhost";
        public bool Simulation => this.environment.Simulation;
        public CancellationToken CancellationToken => this.ExecuterContext?.CancellationToken ?? default;
        public ExecutionStatus ExecutionStatus => this.ExecuterContext?.ExecutionStatus ?? ExecutionStatus.Normal;

        int? IVariableFunctionContext.ProjectId => null;
        int? IVariableFunctionContext.EnvironmentId => null;
        int? IVariableFunctionContext.ExecutionId => this.ExecutionId;
        int? IVariableFunctionContext.ServerId => 1;

        public RompExecutionContext WithExecuterContext(IExecuterContext executerContext) => new RompExecutionContext(this) { ExecuterContext = executerContext };
        public RompExecutionContext WithDirectory(string directory) => new RompExecutionContext(this) { workingDirectoryOverride = PathEx.Combine(this.WorkingDirectory, directory) };

        public Task<string> ApplyTextTemplateAsync(string text, IReadOnlyDictionary<string, RuntimeValue> additionalVariables)
        {
            var template = TextTemplate.Parse(text);
            var error = template.Errors.FirstOrDefault();
            if (error != null)
                throw new ExecutionFailureException("Error applying template: " + error.Message);

            var env = new TextTemplateEnvironment
            {
                ExternalContext = this,
                VariableContext = new RompVariableEvaluationContext(this, this.ExecuterContext),
                AdditionalVariables = additionalVariables
            };

            if (this.ExecuterContext?.LogScope != null)
                env.Log = this.ExecuterContext.LogScope.Log;

            return template.EvaluateAsync(env);
        }

        public RuntimeValue ExpandVariables(string text) => this.ExpandVariablesAsync(text).GetAwaiter().GetResult();
        public Task<RuntimeValue> ExpandVariablesAsync(string text)
        {
            var ps = ProcessedString.Parse(text);
            return ps.EvaluateAsync(new RompVariableEvaluationContext(this, this.ExecuterContext));
        }

        public Agent GetAgent(string serverName)
        {
            if (string.Equals(serverName, "localhost", StringComparison.OrdinalIgnoreCase) || string.Equals(serverName, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                return this.environment.LocalAgent;

            throw new ExecutionFailureException("Romp only supports using the local server.");
        }
        public Task<Agent> GetAgentAsync(string serverName) => Task.FromResult(this.GetAgent(serverName));

        public string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return this.WorkingDirectory;

            var fileOps = this.environment.LocalAgent.GetService<IFileOperationsExecuter>();
            if (path.StartsWith("~\\") || path.StartsWith("~/"))
            {
                var baseDirectory = PathEx.Combine(fileOps.GetBaseWorkingDirectory(), "_E" + this.ExecutionId);
                return PathEx.MakeCanonical(fileOps.CombinePath(baseDirectory, path.Substring(2)), fileOps.DirectorySeparator);
            }

            return PathEx.MakeCanonical(fileOps.CombinePath(this.WorkingDirectory, path), fileOps.DirectorySeparator);
        }

        public void SetVariableValue(string variableName, RuntimeValue variableValue) => this.ExecuterContext.SetVariableValue(new RuntimeVariableName(variableName, RuntimeValueType.Scalar), variableValue);
        public void SetVariableValue(RuntimeVariableName variableName, RuntimeValue variableValue) => this.ExecuterContext.SetVariableValue(variableName, variableValue);

        public RuntimeValue? TryGetFunctionValue(string functionName, params RuntimeValue[] args)
        {
            if (functionName == null)
                throw new ArgumentNullException(nameof(functionName));
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            try
            {
                return new RompVariableEvaluationContext(this, this.ExecuterContext).TryEvaluateFunction(new RuntimeVariableName(functionName, RuntimeValueType.Scalar), args);
            }
            catch
            {
                return null;
            }
        }

        public RuntimeValue? TryGetVariableValue(RuntimeVariableName variableName)
        {
            if (variableName == null)
                throw new ArgumentNullException(nameof(variableName));

            return this.ExecuterContext?.GetVariableValue(variableName);
        }

        private string DetermineWorkingDirectory()
        {
            if (!string.IsNullOrEmpty(this.workingDirectoryOverride) && PathEx.IsPathRooted(this.workingDirectoryOverride))
                return this.workingDirectoryOverride;

            var work = this.workingDirectoryOverride;
            if (!string.IsNullOrEmpty(work) && (work.StartsWith("~\\") || work.StartsWith("~/")))
                work = this.workingDirectoryOverride.Substring(2);

            var executionDirectory = PackageInstaller.TargetDirectory;
            if (string.IsNullOrWhiteSpace(executionDirectory))
                executionDirectory = PathEx.Combine(this.Agent.GetService<IFileOperationsExecuter>().GetBaseWorkingDirectory(), "_E" + this.ExecutionId);

            if (string.IsNullOrEmpty(work))
                return executionDirectory;
            else
                return PathEx.Combine(executionDirectory, work);
        }
    }
}
