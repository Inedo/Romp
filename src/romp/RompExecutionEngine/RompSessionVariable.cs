using System;
using System.Collections.Generic;
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
    }
}
