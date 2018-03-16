using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
using Inedo.Romp.RompPack;

namespace Inedo.Romp.Extensions.Functions
{
    [ScriptAlias("PackageGroup")]
    public sealed class PackageGroupVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IVariableFunctionContext context) => PackageInstaller.PackageId.Group;
    }
}
