using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Parser;
using Inedo.ExecutionEngine.Parser.Processor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inedo.Romp.RompPack
{
    internal sealed class RompPackInfo
    {
        private RompPackInfo(UpackMetadata upack, Dictionary<string, PackageVariable> variables, Dictionary<string, PackageCredentials> credentials, string installScript)
        {
            this.Group = upack.Group;
            this.Name = upack.Name;
            this.Version = upack.Version;
            this.Variables = new ReadOnlyDictionary<string, PackageVariable>(variables);
            this.Credentials = new ReadOnlyDictionary<string, PackageCredentials>(credentials);
            this.RawInstallScript = installScript;
            this.InstallScript = Compiler.CompileText(installScript ?? string.Empty);
        }

        public string Group { get; }
        public string Name { get; }
        public string FullName => string.IsNullOrEmpty(this.Group) ? this.Name : (this.Group + "/" + this.Name);
        public string Version { get; }
        public IReadOnlyDictionary<string, PackageVariable> Variables { get; }
        public IReadOnlyDictionary<string, PackageCredentials> Credentials { get; }
        public string RawInstallScript { get; }
        public ScriptProcessorOutput InstallScript { get; }

        public static RompPackInfo Load(string packageFileName)
        {
            if (string.IsNullOrEmpty(packageFileName))
                throw new ArgumentNullException(nameof(packageFileName));

            using (var stream = File.OpenRead(packageFileName))
            {
                return Load(stream);
            }
        }
        public static RompPackInfo Load(Stream packageStream)
        {
            if (packageStream == null)
                throw new ArgumentNullException(nameof(packageStream));

            using (var zip = new ZipArchive(packageStream, ZipArchiveMode.Read, true))
            {
                var upackEntry = zip.GetEntry("upack.json");
                if (upackEntry == null)
                    throw new RompException("Missing upack.json file in package.");

                UpackMetadata upackInfo;

                using (var upackStream = upackEntry.Open())
                using (var streamReader = new StreamReader(upackStream, InedoLib.UTF8Encoding))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    try
                    {
                        upackInfo = new JsonSerializer().Deserialize<UpackMetadata>(jsonReader);
                    }
                    catch (Exception ex)
                    {
                        throw new RompException("Invalid upack.json file: JSON syntax error.", ex);
                    }
                }

                if (string.IsNullOrWhiteSpace(upackInfo.Name))
                    throw new RompException("Invalid upack.json file: \"name\" property is missing or invalid.");
                if (string.IsNullOrWhiteSpace(upackInfo.Version))
                    throw new RompException("Invalid upack.json file: \"version\" property is missing or invalid.");

                var packageVariables = new Dictionary<string, PackageVariable>(StringComparer.OrdinalIgnoreCase);

                var varEntry = zip.GetEntry("packageVariables.json");
                if (varEntry != null)
                {
                    try
                    {
                        JObject varObj;

                        using (var varStream = varEntry.Open())
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

                var credsEntry = zip.GetEntry("packageCredentials.json");
                if (credsEntry != null)
                {
                    try
                    {
                        JArray credsArray;

                        using (var credsStream = credsEntry.Open())
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
                var installScriptEntry = zip.GetEntry("install.otter");
                if (installScriptEntry != null)
                {
                    using (var installScriptStream = installScriptEntry.Open())
                    using (var streamReader = new StreamReader(installScriptStream, InedoLib.UTF8Encoding))
                    {
                        installScript = streamReader.ReadToEnd();
                    }
                }

                return new RompPackInfo(upackInfo, packageVariables, packageCredentials, installScript);
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

        private sealed class UpackMetadata
        {
            [JsonProperty("group")]
            public string Group { get; set; }
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("version")]
            public string Version { get; set; }
        }
    }
}
