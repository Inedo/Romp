using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Parser;
using Inedo.ExecutionEngine.Parser.Processor;
using Inedo.UPack.Packaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inedo.Romp.RompPack
{
    internal sealed class RompPackInfo
    {
        private RompPackInfo(Dictionary<string, PackageVariable> variables, Dictionary<string, PackageCredentials> credentials, string installScript)
        {
            this.Variables = new ReadOnlyDictionary<string, PackageVariable>(variables);
            this.Credentials = new ReadOnlyDictionary<string, PackageCredentials>(credentials);
            this.RawInstallScript = installScript;
            this.InstallScript = Compiler.CompileText(installScript ?? string.Empty);
        }

        public IReadOnlyDictionary<string, PackageVariable> Variables { get; }
        public IReadOnlyDictionary<string, PackageCredentials> Credentials { get; }
        public string RawInstallScript { get; }
        public ScriptProcessorOutput InstallScript { get; }

        public static RompPackInfo Load(UniversalPackage package)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));

            {
                var packageVariables = new Dictionary<string, PackageVariable>(StringComparer.OrdinalIgnoreCase);

                var varEntry = package.GetRawEntry("packageVariables.json");
                if (varEntry != null)
                {
                    try
                    {
                        JObject varObj;

                        using (var varStream = varEntry.Value.Open())
                        using (var streamReader = new StreamReader(varStream, InedoLib.UTF8Encoding))
                        using (var jsonReader = new JsonTextReader(streamReader))
                        {
                            varObj = JObject.Load(jsonReader);
                        }

                        foreach (var prop in varObj.Properties())
                            packageVariables[prop.Name] = new PackageVariable(prop.Value);
                    }
                    catch (Exception ex)
                    {
                        throw new RompException("Invalid packageVariables.json file.", ex);
                    }
                }

                var packageCredentials = new Dictionary<string, PackageCredentials>(StringComparer.OrdinalIgnoreCase);

                var credsEntry = package.GetRawEntry("packageCredentials.json");
                if (credsEntry != null)
                {
                    try
                    {
                        JArray credsArray;

                        using (var credsStream = credsEntry.Value.Open())
                        using (var streamReader = new StreamReader(credsStream, InedoLib.UTF8Encoding))
                        using (var jsonReader = new JsonTextReader(streamReader))
                        {
                            credsArray = JArray.Load(jsonReader);
                        }

                        foreach (var token in credsArray)
                        {
                            if (!(token is JObject obj))
                                throw new RompException("Invalid token in packageCredentials.json file.");

                            var creds = new PackageCredentials(obj);
                            packageCredentials[creds.Type + "::" + creds.Name] = creds;
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new RompException("Invalid packageCredentials.json file.", ex);
                    }
                }

                string installScript = null;
                var installScriptEntry = package.GetRawEntry("install.otter");
                if (installScriptEntry != null)
                {
                    using (var installScriptStream = installScriptEntry.Value.Open())
                    using (var streamReader = new StreamReader(installScriptStream, InedoLib.UTF8Encoding))
                    {
                        installScript = streamReader.ReadToEnd();
                    }
                }

                return new RompPackInfo(packageVariables, packageCredentials, installScript);
            }
        }

        public bool WriteScriptErrors()
        {
            foreach (var error in this.InstallScript.Errors)
            {
                var message = $"[{error.Level.ToString().ToLowerInvariant()}] {error.Message} (at line #{error.LineNumber})";
                if (error.Level == ScriptErrorLevel.Warning)
                    RompConsoleMessenger.WriteDirect(message, ConsoleColor.Yellow);
                else
                    RompConsoleMessenger.WriteDirect(message, ConsoleColor.Red);
            }

            return this.InstallScript.Errors.Any(e => e.Level == ScriptErrorLevel.Error);
        }
    }
}
