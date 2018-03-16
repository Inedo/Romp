using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;

namespace Inedo.Romp.RompExecutionEngine
{
    internal sealed class RompSessionVariable : IRuntimeVariable
    {
        private static Dictionary<string, RompSessionVariable> variables = new Dictionary<string, RompSessionVariable>(StringComparer.OrdinalIgnoreCase);
        private RuntimeValue value;

        public RompSessionVariable(RuntimeVariableName name, RuntimeValue value)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (name.Type != value.ValueType)
                throw new ArgumentException("Invalid variable value type.");

            this.Name = name;
            this.value = value;
        }

        public RuntimeVariableName Name { get; }

        public RuntimeValue GetValue() => this.value;

        public static void SetSessionVariable(string name, string value) => variables[name] = new RompSessionVariable(new RuntimeVariableName(name, RuntimeValueType.Scalar), value);
        public static void SetSessionVariable(string name, RuntimeValue value) => variables[name] = new RompSessionVariable(new RuntimeVariableName(name, value.ValueType), value);
        public static RompSessionVariable GetSessionVariable(RuntimeVariableName name) => variables.GetValueOrDefault(name.Name);
        public static async Task ExpandValuesAsync(RompExecutionContext context)
        {
            foreach (var var in variables)
            {
                try
                {
                    var.Value.value = await ExpandValueAsync(var.Value.value, context);
                }
                catch (Exception ex)
                {
                    throw new ExecutionFailureException($"Error expanding variables in \"{var.Key}\" session variable: {ex.Message}");
                }
            }
        }

        private static async Task<RuntimeValue> ExpandValueAsync(RuntimeValue value, RompExecutionContext context)
        {
            switch (value.ValueType)
            {
                case RuntimeValueType.Scalar:
                    return await context.ExpandVariablesAsync(value.AsString());

                case RuntimeValueType.Vector:
                    var list = new List<RuntimeValue>();
                    foreach (var item in value.AsEnumerable())
                        list.Add(await ExpandValueAsync(item, context));
                    return new RuntimeValue(list);

                case RuntimeValueType.Map:
                    var map = new Dictionary<string, RuntimeValue>();
                    foreach (var pair in value.AsDictionary())
                        map.Add(pair.Key, await ExpandValueAsync(pair.Value, context));
                    return new RuntimeValue(map);

                default:
                    throw new ArgumentException();
            }
        }
    }
}
