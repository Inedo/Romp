using System.ComponentModel;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Romp.Extensions.Functions
{
    [ScriptAlias("EscapeSqlString")]
    [DisplayName("Escape SQL String")]
    [Description("Returns a string suitable for use in a T-SQL single-quoted string value.")]
    public sealed class EscapeSqlStringFunction : ScalarVariableFunction
    {
        [DisplayName("value")]
        [VariableFunctionParameter(0)]
        [Description("The string to escape.")]
        public string Value { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context) => this.Value?.Replace("'", "''") ?? string.Empty;
    }
}
