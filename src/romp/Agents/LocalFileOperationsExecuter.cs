using Inedo.Agents;
using Inedo.Romp.Configuration;

namespace Inedo.Romp.Agents
{
    internal sealed class RompFileOperationsExecuter : LocalFileOperationsExecuter
    {
        public override string GetBaseWorkingDirectory() => RompConfig.DefaultWorkingDirectory;
    }
}
