using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;

namespace Inedo.Romp
{
    internal static class ExtensionsManager
    {
        private static readonly LazyAsync<InedoExtensionsManager> manager = new LazyAsync<InedoExtensionsManager>(InitializeExtensionsManager, () => Task.Factory.StartNew(InitializeExtensionsManager));

        public static string ExtensionsPath { get; private set; }
        public static string ExtensionsTempPath { get; private set; }
        public static string BuiltInExtensionsPath { get; private set; }

        public static AssemblyName[] RedirectedAssemblies => manager.Value.RedirectedAssemblies;

        public static void SetEnvironmentConfiguration(string extensionsPath, string extensionsTempPath, string builtInExtensionsPath)
        {
            ExtensionsPath = extensionsPath;
            ExtensionsTempPath = extensionsTempPath;
            BuiltInExtensionsPath = builtInExtensionsPath;
        }

        public static void WaitForInitialization()
        {
            var e = manager.Value;
        }
        public static Task WaitForInitializationAsync() => manager.ValueAsync;
        public static void EnableAssemblyBinding() => CoreRedirection.EnableAssemblyBinding(manager.Value.RedirectedAssemblies);

        public static bool IsExtensibleType(Type type) => manager.Value.IsExtensibleType(type);
        public static Type GetComponentBaseType(Type componentType) => manager.Value.GetComponentBaseType(componentType);
        public static IEnumerable<InedoExtension> GetExtensions(bool includeBuiltIn = true)
        {
            var ext = manager.Value.GetExtensions();
            if (!includeBuiltIn)
                ext = ext.Where(e => e.Name != "Inedo SDK" && e.Name != "Romp");

            return ext;
        }
        public static ExtensionComponent GetComponent(Type componentType) => manager.Value.GetComponent(componentType);
        public static IEnumerable<ExtensionComponent> GetAllComponents(ExtensionComponentFilterOptions options = ExtensionComponentFilterOptions.Default) => manager.Value.GetAllComponents(options);
        public static IEnumerable<ExtensionComponent> GetComponentsByBaseClass<TExtensible>(ExtensionComponentFilterOptions options = ExtensionComponentFilterOptions.Default) => manager.Value.GetComponentsByBaseClass<TExtensible>(options);
        public static Type TryGetOperation(string scriptNamespace, string scriptAlias) => GetComponentByScriptAlias<Inedo.Extensibility.Operations.Operation>(scriptNamespace, scriptAlias);

        public static Type TryGetVariableFunction(string scriptAlias) => GetComponentByScriptAlias<Inedo.Extensibility.VariableFunctions.VariableFunction>(null, scriptAlias);
        private static Type GetComponentByScriptAlias<TComponent>(string scriptNamespace, string scriptAlias)
        {
            if (string.IsNullOrWhiteSpace(scriptAlias))
                throw new ArgumentNullException(nameof(scriptAlias));

            var types = from c in GetComponentsByBaseClass<TComponent>(ExtensionComponentFilterOptions.IncludeDeprecated)
                        let t = c.ComponentType
                        let aliases = from a in (ScriptAliasAttribute[])Attribute.GetCustomAttributes(t, typeof(ScriptAliasAttribute))
                                      select a.Alias
                        let ns = c.ComponentType.GetCustomAttribute<ScriptNamespaceAttribute>()?.Namespace
                                 ?? c.ComponentType.Assembly.GetCustomAttribute<ScriptNamespaceAttribute>()?.Namespace
                                 ?? c.ComponentType.Assembly.GetName().Name
                        where aliases.Contains(scriptAlias, OtterScriptSymbolComparer.Instance)
                        where string.IsNullOrWhiteSpace(scriptNamespace) || scriptNamespace.Equals(ns, StringComparison.OrdinalIgnoreCase)
                        select t;

            return types.FirstOrDefault();
        }
        private static InedoExtensionsManager InitializeExtensionsManager()
        {
            var manager = new InedoExtensionsManager(
                new ExtensionsManagerConfiguration
                {
                    Core = typeof(Factory).Assembly,
                    RedirectedAssemblyNames = new[] { "Inedo.SDK", "Inedo.ExecutionEngine", "InedoLib", "Inedo.Agents.Client", "Inedo.Agents" },
                    ExtensionsPath = ExtensionsPath,
                    ExtensionsTempPath = ExtensionsTempPath,
                    BuiltInExtensionsPath = BuiltInExtensionsPath,
                    FileExtension = ".inedox",
                    ExtensibleTypes = new[]
                    {
                        typeof(Inedo.Extensibility.Operations.Operation),
                        typeof(Inedo.Extensibility.VariableFunctions.VariableFunction),
                        typeof(Inedo.Extensibility.Credentials.ResourceCredentials),
                        typeof(Inedo.Extensibility.ListVariableSources.ListVariableSource)
                    }
                }
            );

            manager.Initialize("Inedo.SDK", "romp");
            return manager;
        }
    }
}
