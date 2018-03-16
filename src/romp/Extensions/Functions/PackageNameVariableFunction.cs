using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
using Inedo.Romp.RompPack;

namespace Inedo.Romp.Extensions.Functions
{
    [ScriptAlias("PackageName")]
    public sealed class PackageNameVariableFunction : ScalarVariableFunction
    {
        protected override object EvaluateScalar(IVariableFunctionContext context) => PackageInstaller.PackageId.Name;
    }
}
