using System;
using System.Collections.Generic;
using Inedo.Diagnostics;
using Newtonsoft.Json;

namespace Inedo.Romp.Configuration
{
    [JsonObject]
    internal sealed class RompConfigValues
    {
        private JSValue<bool?> storeLogs;
        private JSValue<bool?> cachePackages;
        private JSValue<bool?> userMode;
        private JSValue<bool?> secureCredentials;
        private JSValue<MessageLevel?> logLevel;
        private JSValue<string> extensionsPath;
        private JSValue<string> extensionsTempPath;

        [JsonProperty("store-logs")]
        public bool? StoreLogs
        {
            get => this.storeLogs.Value;
            set => this.storeLogs = new JSValue<bool?>(value);
        }

        [JsonProperty("log-level", ItemConverterType = typeof(MessageLevelConverter))]
        public MessageLevel? LogLevel
        {
            get => this.logLevel.Value;
            set => this.logLevel = new JSValue<MessageLevel?>(value);
        }

        [JsonProperty("cache-packages")]
        public bool? CachePackages
        {
            get => this.cachePackages.Value;
            set => this.cachePackages = new JSValue<bool?>(value);
        }

        [JsonProperty("user-mode")]
        public bool? UserMode
        {
            get => this.userMode.Value;
            set => this.userMode = new JSValue<bool?>(value);
        }

        [JsonProperty("secure-credentials")]
        public bool? SecureCredentials
        {
            get => this.secureCredentials.Value;
            set => this.secureCredentials = new JSValue<bool?>(value);
        }

        [JsonProperty("extensions-path")]
        public string ExtensionsPath
        {
            get => this.extensionsPath.Value;
            set => this.extensionsPath = new JSValue<string>(value);
        }

        [JsonProperty("extensions-temp-path")]
        public string ExtensionsTempPath
        {
            get => this.extensionsTempPath.Value;
            set => this.extensionsTempPath = new JSValue<string>(value);
        }

        [NotCascaded]
        [JsonProperty("rafts")]
        public Dictionary<string, string> Rafts { get; set; }

        public static RompConfigValues Merge(RompConfigValues a, RompConfigValues b, bool coalesceNull)
        {
            if (ReferenceEquals(a, b))
                return a;
            if (ReferenceEquals(a, null))
                return b;
            if (ReferenceEquals(b, null))
                return a;

            var result = new RompConfigValues
            {
                storeLogs = a.storeLogs.Coalesce(b.storeLogs, coalesceNull),
                cachePackages = a.cachePackages.Coalesce(b.cachePackages, coalesceNull),
                userMode = a.userMode.Coalesce(b.userMode, coalesceNull),
                secureCredentials = a.secureCredentials.Coalesce(b.secureCredentials, coalesceNull),
                logLevel = a.logLevel.Coalesce(b.logLevel, coalesceNull),
                extensionsPath = a.extensionsPath.Coalesce(b.extensionsPath, coalesceNull),
                extensionsTempPath = a.extensionsTempPath.Coalesce(b.extensionsTempPath, coalesceNull),
                Rafts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };

            if (b.Rafts != null)
            {
                foreach (var r in b.Rafts)
                    result.Rafts[r.Key] = r.Value;
            }

            if (a.Rafts != null)
            {
                foreach (var r in a.Rafts)
                    result.Rafts[r.Key] = r.Value;
            }

            return result;
        }
    }
}
