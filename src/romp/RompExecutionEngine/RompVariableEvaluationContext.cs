using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;
using Inedo.ExecutionEngine.Variables;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Romp.RompExecutionEngine
{
    internal sealed class RompVariableEvaluationContext : IVariableEvaluationContext
    {
        private IExecuterContext executerContext;

        public RompVariableEvaluationContext(IVariableFunctionContext functionContext, IExecuterContext executerContext)
        {
            this.VariableFunctionContext = functionContext;
            this.executerContext = executerContext;
        }

        public IVariableFunctionContext VariableFunctionContext { get; }

        public RuntimeValue? TryEvaluateFunction(RuntimeVariableName functionName, IList<RuntimeValue> arguments)
        {
            var function = this.GetVariableFunctionInternal(functionName, arguments);
            return function?.Evaluate(this.VariableFunctionContext);
        }

        public async Task<RuntimeValue?> TryEvaluateFunctionAsync(RuntimeVariableName functionName, IList<RuntimeValue> arguments)
        {
            var function = this.GetVariableFunctionInternal(functionName, arguments);
            if (function == null)
                return null;

            if (function is IAsyncVariableFunction asyncFunc)
                return await asyncFunc.EvaluateAsync(this.VariableFunctionContext).ConfigureAwait(false);
            else
                return function.Evaluate(this.VariableFunctionContext);
        }

        public RuntimeValue? TryGetVariableValue(RuntimeVariableName variableName)
        {
            if (variableName == null)
                throw new ArgumentNullException(nameof(variableName));

            var maybeValue = this.executerContext?.GetVariableValue(variableName);
            if (maybeValue != null)
                return maybeValue;

            return RompSessionVariable.GetSessionVariable(variableName)?.GetValue();
        }

        private ExtensionComponent GetFunction(RuntimeVariableName functionName)
        {
            if (functionName == null)
                throw new ArgumentNullException(nameof(functionName));

            var functionType = (from c in ExtensionsManager.GetComponentsByBaseClass<VariableFunction>()
                                let aliases = from a in c.ComponentType.GetCustomAttributes<ScriptAliasAttribute>()
                                              select a.Alias
                                where aliases.Contains(functionName.Name, StringComparer.OrdinalIgnoreCase)
                                select c).FirstOrDefault();

            return functionType;
        }

        private VariableFunction GetVariableFunctionInternal(RuntimeVariableName functionName, IList<RuntimeValue> arguments)
        {
            if (functionName == null)
                throw new ArgumentNullException(nameof(functionName));
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            var functionType = this.GetFunction(functionName);
            if (functionType == null)
                return null;

            var function = (VariableFunction)Activator.CreateInstance(functionType.ComponentType);

            var functionParams = (from p in functionType.ComponentType.GetProperties()
                                  let a = p.GetCustomAttribute<VariableFunctionParameterAttribute>()
                                  where a != null
                                  let n = p.GetCustomAttribute<DisplayNameAttribute>()
                                  orderby a.Index
                                  select new { Property = p, a.Optional, n?.DisplayName }).ToList();

            int maxParams = Math.Min(functionParams.Count, arguments.Count);
            for (int i = 0; i < maxParams; i++)
            {
                var argValue = arguments[i];
                var param = functionParams[i];
                var coercedValue = ScriptPropertyMapper.CoerceValue(argValue, param.Property);
                param.Property.SetValue(function, coercedValue);
            }

            if (maxParams < functionParams.Count)
            {
                var missing = functionParams
                    .Skip(maxParams)
                    .FirstOrDefault(p => !p.Optional);

                if (missing != null)
                    throw new VariableFunctionArgumentMissingException(missing.DisplayName ?? missing.Property.Name);
            }

            var variadicAttr = functionType.ComponentType.GetCustomAttribute<VariadicVariableFunctionAttribute>();
            if (variadicAttr != null)
            {
                var variadicProperty = functionType.ComponentType.GetProperty(variadicAttr.VariadicPropertyName);
                if (variadicProperty != null)
                {
                    var enumerableType = ScriptPropertyMapper.GetEnumerableType(variadicProperty.PropertyType);
                    if (enumerableType != null)
                    {
                        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(enumerableType));
                        foreach (var arg in arguments.Skip(maxParams))
                            list.Add(ScriptPropertyMapper.CoerceValue(arg, variadicProperty, enumerableType));

                        variadicProperty.SetValue(function, list);
                    }
                }
            }

            return function;
        }
    }
}
