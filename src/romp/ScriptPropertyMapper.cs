using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;
using Inedo.ExecutionEngine.Mapping;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.VariableFunctions;
using Inedo.Romp.RompExecutionEngine;
using RubbishMapper = Inedo.Extensibility.Operations.ICustomArgumentMapper;

namespace Inedo.Romp
{
    public static class ScriptPropertyMapper
    {
        private static readonly CoreMapper core = new CoreMapper();

        public static void SetProperties(object target, ActionStatement action, IVariableFunctionContext bmContext, IExecuterContext executerContext)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var variableContext = new RompVariableEvaluationContext(bmContext, executerContext);
            core.SetProperties(target, action, variableContext, null);

            (target as IHasCredentials)?.SetValues();
            (core.GetTemplate(target) as IHasCredentials)?.SetValues();
        }

        public static void SetNamedProperties(object target, IEnumerable<KeyValuePair<string, RuntimeValue>> arguments)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            core.SetNamedProperties(target, arguments);
        }

        public static void ReadOutputs(object target, IEnumerable<KeyValuePair<string, RuntimeVariableName>> variables, IExecuterContext executerContext) => core.ReadOutputs(target, variables, executerContext);

        internal static object CoerceValue(RuntimeValue value, PropertyInfo property, Type type = null) => core.CoerceValue(value, property, type);

        internal static Type GetEnumerableType(Type type)
        {
            if (!type.IsGenericType)
                return null;

            if (type.GetGenericTypeDefinition() != typeof(IEnumerable<>))
                return null;

            return type.GenericTypeArguments[0];
        }

        private sealed class CoreMapper : CoreScriptPropertyMapper
        {
            protected override Type TemplateContainerType => typeof(ITemplateContainer<>);

            protected override IEnumerable<string> GetScriptAliases(PropertyInfo property) => property.GetCustomAttributes<ScriptAliasAttribute>().Select(a => a.Alias);
            protected override bool IsOutputProperty(PropertyInfo property) => Attribute.IsDefined(property, typeof(OutputAttribute));
            protected override bool ShouldExpandVariables(PropertyInfo property) => !Attribute.IsDefined(property, typeof(DisableVariableExpansionAttribute));
            protected override PropertyInfo GetTemplateContainerTemplateProperty(Type templateType) => typeof(ITemplateContainer<>).MakeGenericType(templateType).GetProperty(nameof(ITemplateContainer<object>.Template));

            public object GetTemplate(object target)
            {
                if (target == null)
                    throw new ArgumentNullException(nameof(target));

                var templateType = (from t in target.GetType().GetInterfaces()
                                    where t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ITemplateContainer<>)
                                    select t.GetGenericArguments()[0]).FirstOrDefault();

                if (templateType == null)
                    return null;

                return this.GetTemplateContainerTemplateProperty(templateType).GetValue(target);
            }

            protected override object CoerceScalarValue(RuntimeValue value, Type type, PropertyInfo property)
            {
                if (type == typeof(TimeSpan))
                {
                    var maybeDouble = value.AsString();
                    if (maybeDouble == null)
                        throw new ValueCoercionException(value, type);

                    if (!double.TryParse(maybeDouble, out double dvalue))
                        throw new ValueCoercionException(value, type);

                    var tsUnit = property.GetCustomAttribute<TimeSpanUnitAttribute>();
                    if (tsUnit != null)
                        return tsUnit.GetTimeSpan(dvalue);

                    return TimeSpan.FromSeconds(dvalue);
                }
                else
                {
                    return base.CoerceScalarValue(value, type, property);
                }
            }
            protected override ICustomArgumentMapper GetCustomArgumentMapper(object instance)
            {
                var mapper = base.GetCustomArgumentMapper(instance);
                if (mapper != null)
                    return mapper;

                if (instance is RubbishMapper rubbish)
                    return new CustomArgumentMapperShim(rubbish);

                return null;
            }

            private sealed class CustomArgumentMapperShim : ICustomArgumentMapper
            {
                private readonly RubbishMapper mapper;

                public CustomArgumentMapperShim(RubbishMapper mapper) => this.mapper = mapper;

                public RuntimeValue DefaultArgument
                {
                    set => this.mapper.DefaultArgument = value;
                }

                public IReadOnlyDictionary<string, RuntimeValue> NamedArguments
                {
                    set => this.mapper.NamedArguments = value;
                }

                public IDictionary<string, RuntimeValue> OutArguments
                {
                    get => this.mapper.OutArguments;
                    set => this.mapper.OutArguments = value;
                }
            }
        }
    }
}
