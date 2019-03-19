using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gibraltar.Agent;
using Gibraltar.Agent.Configuration;
using Inedo.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inedo.Romp.Configuration
{
    internal static class RompConfig
    {
        private static readonly string UserConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".romp");
        private static readonly string MachineConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Romp");

        public static RompConfigValues Values { get; private set; }
        public static RompConfigValues OverriddenValues { get; private set; }

        public static string ConfigDataPath => UserMode ? UserConfigPath : MachineConfigPath;
        public static string DataFilePath => Path.Combine(ConfigDataPath, "romp.sqlite3");
        public static string ExtensionsPath => Values.ExtensionsPath;
        public static string ExtensionsTempPath => Values.ExtensionsTempPath;
        public static string DefaultWorkingDirectory => Path.Combine(ConfigDataPath, "temp", "jobs");
        public static bool UserMode => Values.UserMode.Value;
        public static IReadOnlyDictionary<string, string> Rafts => Values.Rafts;
        public static bool StoreLogs => Values.StoreLogs.Value;
        public static MessageLevel LogLevel => Values.LogLevel.Value;
        public static string DefaultSource => Values.DefaultSource;
        public static bool CeipEnabled => Values.CeipEnabled ?? false;

        public static void Initialize(ArgList args)
        {
            var configArgs = ParseConfigArguments(args);

            var overridden = MungeConfig(
                configArgs,
                LoadConfigFile(Path.Combine(Environment.CurrentDirectory, "rompconfig.json")),
                LoadConfigFile(Path.Combine(UserConfigPath, "rompconfig.json")),
                LoadConfigFile(Path.Combine(MachineConfigPath, "rompconfig.json"))
            );

            OverriddenValues = overridden;
            Values = RompConfigValues.Merge(overridden, getDefaults(), true);

            if (CeipEnabled)
                InitializeLoupe();

            RompConfigValues getDefaults()
            {
                // some defaults are dependent on whether running in user mode or not
                var path = overridden.UserMode == true ? UserConfigPath : MachineConfigPath;

                return new RompConfigValues
                {
                    StoreLogs = true,
                    LogLevel = MessageLevel.Warning,
                    CachePackages = true,
                    UserMode = false,
                    SecureCredentials = false,
                    ExtensionsPath = Path.Combine(path, "extensions"),
                    ExtensionsTempPath = Path.Combine(path, "temp", "extensions"),
                    Rafts = new Dictionary<string, string>()
                };
            }
        }
        public static void SetValue(string name, string value)
        {
            var path = Path.Combine(UserMode == true ? UserConfigPath : MachineConfigPath, "rompconfig.json");
            JObject obj;
            if (File.Exists(path))
            {
                using (var reader = new StreamReader(path, InedoLib.UTF8Encoding))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    obj = JObject.Load(jsonReader);
                }
            }
            else
            {
                obj = new JObject();
            }

            obj["name"] = value;

            using (var writer = new StreamWriter(path, false, InedoLib.UTF8Encoding, 16))
            using (var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented })
            {
                obj.WriteTo(jsonWriter);
            }
        }
        public static void DeleteValue(string name)
        {
            var path = Path.Combine(UserMode == true ? UserConfigPath : MachineConfigPath, "rompconfig.json");
            JObject obj;
            if (File.Exists(path))
            {
                using (var reader = new StreamReader(path, InedoLib.UTF8Encoding))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    obj = JObject.Load(jsonReader);
                }
            }
            else
            {
                obj = new JObject();
            }

            if (obj.Remove(name))
            {
                using (var writer = new StreamWriter(path, false, InedoLib.UTF8Encoding, 16))
                using (var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented })
                {
                    obj.WriteTo(jsonWriter);
                }
            }
        }

        private static RompConfigValues ParseConfigArguments(ArgList args)
        {
            var config = new RompConfigValues
            {
                Rafts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };

            var properties = typeof(RompConfigValues)
                .GetProperties()
                .Where(p => !Attribute.IsDefined(p, typeof(NotCascadedAttribute)))
                .ToDictionary(p => p.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName, StringComparer.OrdinalIgnoreCase);

            args.ProcessOptions(
                o =>
                {
                    if (string.Equals(o.Key, "log-level", StringComparison.OrdinalIgnoreCase))
                    {
                        switch (o.Value?.ToLowerInvariant())
                        {
                            case "debug":
                                config.LogLevel = MessageLevel.Debug;
                                break;
                            case "info":
                            case "information":
                                config.LogLevel = MessageLevel.Information;
                                break;
                            case "warn":
                            case "warning":
                                config.LogLevel = MessageLevel.Warning;
                                break;
                            case "error":
                                config.LogLevel = MessageLevel.Error;
                                break;
                            default:
                                throw new RompException("Invalid log-level; must be debug, info, warn, or error.");
                        }

                        return true;
                    }

                    var p = properties.GetValueOrDefault(o.Key);
                    if (p != null)
                    {
                        try
                        {
                            p.SetValue(config, Convert.ChangeType(o.Value, Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType));
                            return true;
                        }
                        catch (Exception ex)
                        {
                            throw new RompException($"Invalid argument: {o.Key}={o.Value}: {ex.Message}", ex);
                        }
                    }

                    return false;
                }
            );

            return config;
        }
        private static RompConfigValues LoadConfigFile(string fileName)
        {
            if (File.Exists(fileName))
            {
                try
                {
                    using (var reader = File.OpenText(fileName))
                    using (var jsonReader = new JsonTextReader(reader))
                    {
                        var serializer = JsonSerializer.Create();
                        return serializer.Deserialize<RompConfigValues>(jsonReader);
                    }
                }
                catch
                {
                }
            }

            return new RompConfigValues();
        }
        private static RompConfigValues MungeConfig(params RompConfigValues[] configs)
        {
            var result = new RompConfigValues();

            foreach (var c in configs)
                result = RompConfigValues.Merge(result, c, false);

            return result;
        }

        private static void InitializeLoupe()
        {
            if (CeipEnabled)
            {
                var config = new AgentConfiguration();
                config.Publisher.ProductName = "Romp";
                config.Publisher.ApplicationName = "Romp";
                config.Publisher.ApplicationVersion = typeof(RompConfig).Assembly.GetName().Version;
                config.Server.CustomerName = "Inedo";
                config.Server.AutoSendSessions = true;
                config.Server.SendAllApplications = true;
                config.Server.UseGibraltarService = true;
                Log.StartSession(config);
            }
        }
    }
}
