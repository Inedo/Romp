using System;
using System.Reflection;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Documentation;
using Inedo.Extensibility.Agents;

namespace Inedo.Romp.Agents
{
    internal sealed class LocalAgent : Agent, IRemoteMethodExecuter, IRemoteProcessExecuter
    {
        public override RichDescription GetDescription() => new RichDescription("Local agent");

        protected override object GetAgentServiceInternal(Type service)
        {
            if (service == typeof(IFileOperationsExecuter))
                return new RompFileOperationsExecuter();
            else if (service == typeof(IRemoteJobExecuter))
                return new LocalJobExecuter();
            else if (service == typeof(IRemoteMethodExecuter) || service == typeof(IRemoteProcessExecuter))
                return this;
            else
                return null;
        }

        IRemoteProcess IRemoteProcessExecuter.CreateProcess(RemoteProcessStartInfo startInfo) => new LocalProcess(startInfo);
        Task<string> IRemoteProcessExecuter.GetEnvironmentVariableValueAsync(string name) => Task.FromResult(Environment.GetEnvironmentVariable(name));
        Task<object> IRemoteMethodExecuter.InvokeMethodAsync(MethodBase method, object instance, object[] parameters) => Task.FromResult(method.Invoke(instance, parameters));
    }
}
