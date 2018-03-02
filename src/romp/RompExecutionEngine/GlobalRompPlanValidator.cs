using System;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Parser;
using Inedo.Extensibility;

namespace Inedo.Romp.RompExecutionEngine
{
    public static class GlobalRompPlanValidator
    {
        public static void Initialize() => Compiler.GlobalValidateStatement += HandleValidateStatement;

        private static void HandleValidateStatement(object sender, ValidateStatementEventArgs e)
        {
            if (e.Statement is SetContextStatement setContext)
            {
                Validate(setContext, e);
                return;
            }

            if (e.Statement is ContextIterationStatement contextIteration)
            {
                Validate(contextIteration, e);
                return;
            }

            if (e.Statement is ActionStatement actionStatement)
            {
                Validate(actionStatement, e);
                return;
            }
        }

        private static void Validate(ActionStatement statement, ValidateStatementEventArgs e)
        {
            Type operationType;
            try
            {
                operationType = ExtensionsManager.TryGetOperation(statement.ActionName.Namespace, statement.ActionName.Name);
                if (operationType == null)
                {
                    e.AddError($"Unknown operation \"{statement.ActionName.FullName}\".");
                    return;
                }
            }
            catch (Exception ex)
            {
                e.AddError($"Unknowable operation \"{statement.ActionName.FullName}\": {ex.Message}.");
                return;
            }

            var defaultPropertyName = operationType.GetCustomAttribute<DefaultPropertyAttribute>()?.Name;

            var properties = from p in operationType.GetProperties()
                             where Attribute.IsDefined(p, typeof(RequiredAttribute)) && !Attribute.IsDefined(p, typeof(DefaultValueAttribute))
                             let aliases = p.GetCustomAttributes<ScriptAliasAttribute>().Select(a => a.Alias).ToList()
                             where aliases.Count > 0
                             select new
                             {
                                 Property = p,
                                 Aliases = aliases
                             };

            foreach (var p in properties)
            {
                if (!p.Aliases.Any(a => statement.Arguments.ContainsKey(a)))
                {
                    if (p.Property.Name != defaultPropertyName || statement.PositionalArguments.Count == 0)
                        e.AddError($"Missing required \"{p.Aliases[0]}\" argument for \"{statement.ActionName.FullName}\" operation.");
                }
            }
        }

        private static void Validate(SetContextStatement statement, ValidateStatementEventArgs e) => Validate(statement.ContextType, e);
        private static void Validate(ContextIterationStatement statement, ValidateStatementEventArgs e) => Validate(statement.ContextType, e);
        private static void Validate(string contextType, ValidateStatementEventArgs e)
        {
            if (!string.Equals(contextType, "server", StringComparison.OrdinalIgnoreCase) && !string.Equals(contextType, "directory", StringComparison.OrdinalIgnoreCase))
                e.AddError($"Invalid context \"{contextType}\" in \"for\" statement. Valid contexts are \"server\" or \"directory\".");
        }
    }
}
