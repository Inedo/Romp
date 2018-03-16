using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
using Inedo.Romp.RompPack;

namespace Inedo.Romp.Extensions.Functions
{
    [ScriptAlias("PackageVersion")]
    public sealed class PackageVersionVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IVariableFunctionContext context) => PackageInstaller.PackageVersion.ToString();
    }
}
