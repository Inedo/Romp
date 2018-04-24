using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Inedo.Configuration;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Romp.Extensions.Operations
{
    [ScriptAlias("Ensure-ConfigFile")]
    [ScriptNamespace("InedoInternal", PreferUnqualified = false)]
    public sealed class EnsureConfigFileOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("File")]
        public string FileName { get; set; }

        [ScriptAlias("ConnectionString")]
        public string ConnectionString { get; set; }
        [ScriptAlias("EncryptionKey")]
        public SecureString EncryptionKey { get; set; }
        [ScriptAlias("WebServerEnabled")]
        public bool? WebServerEnabled { get; set; }
        [ScriptAlias("WebServerUrls")]
        public string WebServerUrls { get; set; }

        public override Task ExecuteAsync(IOperationExecutionContext context)
        {
            var fullPath = context.ResolvePath(this.FileName);
            this.LogDebug($"Checking for existing config file at {fullPath}...");

            var configFile = new InedoProductConfigurationFile();
            if (File.Exists(fullPath))
            {
                this.LogDebug("Config file already exists; loading...");
                try
                {
                    configFile = InedoProductConfigurationFile.Load(fullPath) ?? new InedoProductConfigurationFile();
                    this.LogDebug("Config file loaded.");
                }
                catch (Exception ex)
                {
                    this.LogWarning("Config file could not be loaded: " + ex.Message);
                }
            }
            else
            {
                this.LogDebug("Config file does not exist; creating a new one...");
            }

            if (!string.IsNullOrWhiteSpace(this.ConnectionString))
                configFile.ConnectionString = this.ConnectionString;
            if (configFile.EncryptionKey?.Length > 0 && this.EncryptionKey != null)
                configFile.EncryptionKey = this.EncryptionKey;
            if (this.WebServerEnabled != null)
                configFile.WebServerEnabled = this.WebServerEnabled.Value;
            if (!string.IsNullOrWhiteSpace(this.WebServerUrls))
                configFile.WebServerUrls = this.WebServerUrls.Split(';').Select(u => u.Trim()).Where(u => u != string.Empty).ToList();

            var dir = Path.GetDirectoryName(fullPath);
            this.LogDebug($"Ensuring directory {dir} exists...");
            Directory.CreateDirectory(dir);

            this.LogInformation($"Writing config file to {fullPath}...");
            configFile.Save(fullPath);

            this.LogInformation("Config file updated.");
            return Complete;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription
            (
                new RichDescription(
                    "Write configuration settings to ",
                    new DirectoryHilite(config[nameof(FileName)])
                )
            );
        }
    }
}
