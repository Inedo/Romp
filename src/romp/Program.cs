using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Romp.Configuration;
using Inedo.Romp.Data;
using Inedo.Romp.RompExecutionEngine;
using Inedo.Romp.RompPack;
using Inedo.Serialization;
using Inedo.UPack;
using Inedo.UPack.Packaging;
using Newtonsoft.Json;

namespace Inedo.Romp
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                RompConsoleMessenger.WriteDirect("romp 2.0.0-M2", ConsoleColor.White);

                var argList = new ArgList(args);

                RompConfig.Initialize(argList);

                RompSdkConfig.Initialize();

                // this is a hack due to the weird way the extensions manager works
                Directory.CreateDirectory(RompConfig.ExtensionsPath);

                ExtensionsManager.SetEnvironmentConfiguration(RompConfig.ExtensionsPath, RompConfig.ExtensionsTempPath, AppDomain.CurrentDomain.BaseDirectory);
                RompDb.Initialize();
                GlobalRompPlanValidator.Initialize();
                Logger.AddMessenger(new RompConsoleMessenger());

                RompConsoleMessenger.MinimumLevel = RompConfig.LogLevel;

                var command = argList.PopCommand()?.ToLowerInvariant();
                switch (command)
                {
                    case "install":
                        await Install(argList);
                        break;
                    case "uninstall":
                        await Uninstall(argList);
                        break;
                    case "validate":
                        Validate(argList);
                        break;
                    case "inspect":
                        await Inspect(argList);
                        break;
                    case "pack":
                        Pack(argList);
                        break;
                    case "sources":
                        Sources(argList);
                        break;
                    case "jobs":
                        Jobs(argList);
                        break;
                    case "credentials":
                        Credentials(argList);
                        break;
                    case "config":
                        Config(argList);
                        break;
                    case "packages":
                        await Packages(argList);
                        break;
                    case "about":
                        About(argList);
                        break;
                    default:
                        WriteUsage();
                        break;
                }

                WaitForEnter();
                return 0;
            }
            catch (RompException ex)
            {
                Console.Error.WriteLine(ex.Message);
                WaitForEnter();
                return -1;
            }
        }

        private static void WriteUsage()
        {
            Console.WriteLine("Usage: romp <command>");
            Console.WriteLine("Commands:");
            Console.WriteLine("install");
            Console.WriteLine("uninstall");
            Console.WriteLine("validate");
            Console.WriteLine("inspect");
            Console.WriteLine("pack");
            Console.WriteLine("sources");
            Console.WriteLine("jobs");
            Console.WriteLine("credentials");
            Console.WriteLine("config");
            Console.WriteLine("packages");
            Console.WriteLine("about");
        }

        private static async Task<RegisteredPackage> GetRegisteredPackageAsync(UniversalPackageId id)
        {
            using (var registry = PackageRegistry.GetRegistry(RompConfig.UserMode))
            {
                await registry.LockAsync();
                var registeredPackage = (await registry.GetInstalledPackagesAsync())
                    .FirstOrDefault(isMatch);

                await registry.UnlockAsync();

                return registeredPackage;

                bool isMatch(RegisteredPackage p)
                {
                    try
                    {
                        var id2 = new UniversalPackageId(p.Group, p.Name);
                        return id == id2;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
        }

        private static async Task Install(ArgList args)
        {
            var spec = PackageSpecifier.FromArgs(args);
            if (spec == null)
                throw new RompException("Usage: romp install <package-file-or-name> [--version=<version-number>] [--source=<name-or-feed-url>] [--simulate] [--force] [-Vvar=value...]");

            Console.WriteLine("Package: " + spec);
            Console.WriteLine();

            await ExtensionsManager.WaitForInitializationAsync();

            bool simulate = false;
            bool force = false;
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var package = await spec.FetchPackageAsync(args, default))
            {
                args.ProcessOptions(parseOption);

                if (!force)
                {
                    var registeredPackage = await GetRegisteredPackageAsync(spec.PackageId);
                    if (registeredPackage != null)
                    {
                        Console.WriteLine("Package is already installed. Use --force to install anyway.");
                        return;
                    }
                }

                foreach (var var in vars)
                    RompSessionVariable.SetSessionVariable(var.Key, var.Value);

                var packageInfo = RompPackInfo.Load(package);
                if (packageInfo.WriteScriptErrors())
                    throw new RompException("Error compiling install script.");

                foreach (var var in packageInfo.Variables)
                {
                    if (var.Value.Value != null && !vars.ContainsKey(var.Key))
                        RompSessionVariable.SetSessionVariable(var.Key, var.Value.Value.Value);
                }

                foreach (var var in packageInfo.Variables)
                {
                    // should also validate/coerce type here
                    if (var.Value.Required && var.Value.Value == null && !vars.ContainsKey(var.Key))
                    {
                        if (Console.IsOutputRedirected)
                            throw new RompException("Missing required variable: " + var.Key);

                        Console.WriteLine($"Variable \"{var.Key}\" is required.");
                        if (!string.IsNullOrWhiteSpace(var.Value.Description))
                            Console.WriteLine("Description: " + var.Value.Description);

                        string value;
                        do
                        {
                            // should not assume type to be scalar
                            Console.Write(new RuntimeVariableName(var.Key, RuntimeValueType.Scalar) + ": ");
                            if (var.Value.Sensitive)
                                value = ReadSensitive();
                            else
                                value = Console.ReadLine();
                        }
                        while (string.IsNullOrEmpty(value));

                        RompSessionVariable.SetSessionVariable(var.Key, value);
                    }
                }

                bool credentialsMissing = false;
                foreach (var creds in packageInfo.Credentials.Values)
                {
                    if (RompDb.GetCredentialsByName(creds.Type, creds.Name) == null)
                    {
                        credentialsMissing = true;
                        var text = "Credentials required: " + creds.FullName;
                        if (!string.IsNullOrWhiteSpace(creds.Description))
                            text += " (" + creds.Description + ")";
                        RompConsoleMessenger.WriteDirect(text, ConsoleColor.Red);
                    }
                }

                if (credentialsMissing)
                    throw new RompException("Use \"romp credentials store\" to create missing credentials.");

                await PackageInstaller.RunAsync(package, "install.otter", simulate);

                using (var registry = PackageRegistry.GetRegistry(RompConfig.UserMode))
                {
                    await registry.LockAsync();

                    await registry.RegisterPackageAsync(
                        new RegisteredPackage
                        {
                            Group = package.Group,
                            Name = package.Name,
                            Version = package.Version.ToString(),
                            InstallationDate = DateTimeOffset.Now.ToString("o"),
                            InstalledBy = Environment.UserName,
                            InstalledUsing = "Romp",
                            InstallPath = PackageInstaller.TargetDirectory
                        }
                    );

                    await registry.UnlockAsync();
                }
            }

            bool parseOption(ArgOption o)
            {
                switch (o.Key.ToLowerInvariant())
                {
                    case "simulate":
                    case "simulation":
                        simulate = true;
                        return true;
                    case "force":
                        force = true;
                        return true;
                }

                if (o.Key.StartsWith("V") && o.Key.Length > 1)
                {
                    vars[o.Key.Substring(1)] = o.Value ?? string.Empty;
                    return true;
                }

                return false;
            }
        }
        private static async Task Uninstall(ArgList args)
        {
            var spec = PackageSpecifier.FromArgs(args);
            if (spec == null)
                throw new RompException("Usage: romp uninstall <package> [-Vvar=value...]");

            Console.WriteLine("Package: " + spec);
            Console.WriteLine();

            var registeredPackage = await GetRegisteredPackageAsync(spec.PackageId);
            if (registeredPackage == null)
                throw new RompException("Package is not installed.");

            spec.PackageVersion = UniversalPackageVersion.Parse(registeredPackage.Version);

            await ExtensionsManager.WaitForInitializationAsync();

            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var package = await spec.FetchPackageAsync(args, default))
            {
                args.ProcessOptions(parseOption);

                foreach (var var in vars)
                    RompSessionVariable.SetSessionVariable(var.Key, var.Value);

                var packageInfo = RompPackInfo.Load(package);
                if (packageInfo.WriteScriptErrors())
                    throw new RompException("Error compiling uninstall script.");

                PackageInstaller.TargetDirectory = registeredPackage.InstallPath;
                RompSessionVariable.SetSessionVariable("TargetDirectory", registeredPackage.InstallPath);

                await PackageInstaller.RunAsync(package, "uninstall.otter", false);

                using (var registry = PackageRegistry.GetRegistry(RompConfig.UserMode))
                {
                    await registry.LockAsync();

                    await registry.UnregisterPackageAsync(registeredPackage);
                    await registry.DeleteFromCacheAsync(spec.PackageId, package.Version);

                    await registry.UnlockAsync();
                }
            }

            bool parseOption(ArgOption o)
            {
                if (o.Key.StartsWith("V") && o.Key.Length > 1)
                {
                    vars[o.Key.Substring(1)] = o.Value ?? string.Empty;
                    return true;
                }

                return false;
            }
        }
        private static void Validate(ArgList args)
        {
            var packageName = args.PopCommand();
            if (string.IsNullOrEmpty(packageName))
                throw new RompException("Usage: romp validate <package-file>");

            using (var package = new UniversalPackage(packageName))
            {
                var packageInfo = RompPackInfo.Load(package);
                if (packageInfo.WriteScriptErrors())
                    throw new RompException("Error compiling install script.");

                Console.WriteLine($"Package {new UniversalPackageId(package.Group, package.Name)} validated.");
            }
        }
        private static async Task Inspect(ArgList args)
        {
            var spec = PackageSpecifier.FromArgs(args);
            if (spec == null)
                throw new RompException("Usage: romp inspect <package-file-or-name> [--version=<version-number>] [--source=<name-or-feed-url>]");

            Console.WriteLine("Package: " + spec);
            Console.WriteLine();

            using (var package = await spec.FetchPackageAsync(args, default))
            {
                var packageInfo = RompPackInfo.Load(package);
                Console.WriteLine("Name: " + new UniversalPackageId(package.Group, package.Name));
                Console.WriteLine("Version: " + package.Version);

                Console.WriteLine();
                if (packageInfo.Variables.Count > 0)
                {
                    Console.WriteLine("Variables:");
                    foreach (var var in packageInfo.Variables)
                        Console.WriteLine($" {var.Key}={var.Value.Value?.ToString() ?? "(required)"}");
                }
                else
                {
                    Console.WriteLine("Variables: (none)");
                }

                Console.WriteLine();
                if (packageInfo.Credentials.Count > 0)
                {
                    Console.WriteLine("Credentials:");
                    foreach (var creds in packageInfo.Credentials)
                        Console.WriteLine($" {creds.Key}: {AH.CoalesceString(creds.Value.Description, "(required)")}");
                }
                else
                {
                    Console.WriteLine("Credentials: (none)");
                }

                Console.WriteLine();
                if (!string.IsNullOrWhiteSpace(packageInfo.RawInstallScript))
                {
                    if (packageInfo.InstallScript.Errors.Count > 0)
                    {
                        Console.WriteLine("install.otter:");
                        packageInfo.WriteScriptErrors();
                    }
                    else
                    {
                        Console.WriteLine("install.otter: no errors");
                    }
                }
                else
                {
                    Console.WriteLine("install.otter: not present");
                }
            }
        }
        private static void Pack(ArgList args)
        {
            var source = args.PopCommand();
            if (string.IsNullOrEmpty(source))
                throw new RompException("Usage: romp pack <source-directory> [output-file-name] [--force] [--overwrite]");

            var packageName = args.PopCommand();

            bool force = false;
            bool overwrite = false;

            args.ProcessOptions(parseOption);
            args.ThrowIfAnyRemaining();

            PackageBuilder.BuildPackage(Path.GetFullPath(source), packageName, force, overwrite);

            bool parseOption(ArgOption o)
            {
                switch (o.Key.ToLowerInvariant())
                {
                    case "force":
                        force = true;
                        return true;
                    case "overwrite":
                        overwrite = true;
                        return true;
                    default:
                        return false;
                }
            }
        }
        private static void Sources(ArgList args)
        {
            var command = args.PopCommand()?.ToLowerInvariant();
            switch (command)
            {
                case "list":
                    list();
                    break;
                case "display":
                    display();
                    break;
                case "create":
                    create();
                    break;
                default:
                    Console.WriteLine("Usage:");
                    Console.WriteLine("romp sources list");
                    Console.WriteLine("romp sources display <name> [--show-hidden]");
                    Console.WriteLine("romp sources create <name> <url>");
                    break;
            }

            void list()
            {
                bool any = false;
                Console.WriteLine("Package sources:");

                foreach (var s in RompDb.GetPackageSources())
                {
                    any = true;
                    var url = s.FeedUrl;
                    if (!string.IsNullOrEmpty(s.UserName))
                        url = s.UserName + "@" + url;
                    Console.WriteLine(" " + s.Name + ": " + url);
                }

                if (!any)
                    Console.WriteLine(" (none)");
            }

            void display()
            {
                var name = args.PopCommand();
                if (string.IsNullOrEmpty(name))
                    throw new RompException("Expected source name.");

                var source = RompDb.GetPackageSources()
                    .FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

                if (source == null)
                    throw new RompException($"Source {name} not found.");

                bool showHidden = false;
                args.ProcessOptions(
                    o =>
                    {
                        if (string.Equals(o.Key, "show-hidden", StringComparison.OrdinalIgnoreCase))
                        {
                            showHidden = true;
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                );

                args.ThrowIfAnyRemaining();

                Console.WriteLine("Name: " + source.Name);
                Console.WriteLine("Url: " + source.FeedUrl);
                Console.WriteLine("User: " + source.UserName ?? "(not specified)");
                if (showHidden)
                    Console.WriteLine("Password: " + AH.Unprotect(source.Password) ?? "(not specified)");
            }

            void create()
            {
                var name = args.PopCommand();
                var url = args.PopCommand();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
                    throw new RompException("Usage: romp sources create <name> <url>");

                Uri uri;
                try
                {
                    uri = new Uri(url);
                }
                catch (Exception ex)
                {
                    throw new RompException("Invalid URL: " + ex.Message, ex);
                }

                string userName = null;
                SecureString password = null;
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var parts = uri.UserInfo.Split(new[] { ':' }, 2);
                    userName = parts[0];
                    password = AH.CreateSecureString(parts.Length > 1 ? parts[1] : string.Empty);
                }

                var sanitizedUrl = new UriBuilder(uri)
                {
                    UserName = null,
                    Password = null
                };

                RompDb.CreateOrUpdatePackageSource(name, sanitizedUrl.ToString(), userName, password);
                Console.WriteLine("Package source stored.");
            }
        }
        private static void Jobs(ArgList args)
        {
            var command = args.PopCommand()?.ToLowerInvariant();
            switch (command)
            {
                case "list":
                    list();
                    break;
                case "logs":
                    logs();
                    break;
                case "purge":
                    purge();
                    break;
                default:
                    Console.WriteLine("Usage:");
                    Console.WriteLine("romp jobs list");
                    Console.WriteLine("romp jobs logs [jobId]");
                    Console.WriteLine("romp jobs purge <days>");
                    break;
            }

            void list()
            {
                args.ThrowIfAnyRemaining();

                var jobs = RompDb.GetExecutions()
                    .OrderByDescending(j => j.StartDate);

                Console.WriteLine("Jobs:");
                bool any = false;

                foreach (var job in jobs)
                {
                    any = true;
                    var text = $" {job.ExecutionId} {job.StartDate.LocalDateTime} {Domains.ExecutionStatus.GetName(job.StatusCode)}";
                    if (job.StatusCode == Domains.ExecutionStatus.Error)
                        RompConsoleMessenger.WriteDirect(text, ConsoleColor.Red);
                    else if (job.StatusCode == Domains.ExecutionStatus.Warning)
                        RompConsoleMessenger.WriteDirect(text, ConsoleColor.Yellow);
                    else
                        RompConsoleMessenger.WriteDirect(text);
                }

                if (!any)
                    Console.WriteLine(" (none)");
            }

            void logs()
            {
                int? jobId = null;
                var jobIdText = args.PopCommand();
                if (!string.IsNullOrEmpty(jobIdText))
                {
                    jobId = AH.ParseInt(jobIdText);
                    if (jobId == null)
                        throw new RompException("Invalid job ID.");
                }

                args.ThrowIfAnyRemaining();

                if (jobId == null)
                {
                    var latest = RompDb.GetExecutions().OrderByDescending(j => j.ExecutionId).FirstOrDefault();
                    jobId = latest?.ExecutionId;
                }

                if (jobId != null)
                {
                    foreach (var log in RompDb.GetExecutionLogs(jobId.Value))
                        log.WriteText(0, Console.Out);
                }
            }

            void purge()
            {
                var daysText = args.PopCommand();
                if (string.IsNullOrEmpty(daysText))
                    throw new RompException("Usage: romp logs purge <days>");

                if (!int.TryParse(daysText, out int days) || days < 0)
                    throw new RompException("Must specify a nonnegative integer for \"days\" argument.");

                var now = DateTimeOffset.Now;
                var executions = RompDb.GetExecutions()
                    .Where(e => (int)now.Subtract(e.StartDate).TotalDays >= days)
                    .ToList();

                Console.WriteLine($"Purging logs for jobs older than {days} days.");

                foreach (var exec in executions)
                {
                    Console.WriteLine($"Purging job #{exec.ExecutionId} ({exec.StartDate.LocalDateTime})...");
                    RompDb.DeleteExecution(exec.ExecutionId);
                }

                Console.WriteLine($"Purged {executions.Count} jobs.");
                Console.WriteLine();
            }
        }
        private static void Credentials(ArgList args)
        {
            var command = args.PopCommand()?.ToLowerInvariant();
            switch (command)
            {
                case "list":
                    list();
                    break;
                case "display":
                    display();
                    break;
                case "store":
                    store();
                    break;
                case "delete":
                    delete();
                    break;
                default:
                    Console.WriteLine("Usage:");
                    Console.WriteLine("romp credentials list");
                    Console.WriteLine("romp credentials display <name> [--show-hidden]");
                    Console.WriteLine("romp credentials store <name>");
                    Console.WriteLine("romp credentials delete <name>");
                    break;
            }

            void list()
            {
                foreach (var c in RompDb.GetCredentials())
                    Console.WriteLine(c.CredentialType_Name + "::" + c.Credential_Name);
            }

            void display()
            {
                var n = parseQualifiedName();
                var creds = RompDb.GetCredentialsByName(n.type, n.name);
                if (creds == null)
                    throw new RompException($"Credentials {n.type}::{n.name} not found.");

                bool showHidden = false;
                args.ProcessOptions(
                    o =>
                    {
                        if (string.Equals(o.Key, "show-hidden", StringComparison.OrdinalIgnoreCase))
                        {
                            showHidden = true;
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                );

                args.ThrowIfAnyRemaining();

                var instance = (ResourceCredentials)Persistence.DeserializeFromPersistedObjectXml(creds.Configuration_Xml);

                Console.WriteLine($"Name: {creds.CredentialType_Name}::{creds.Credential_Name}");

                foreach (var prop in Persistence.GetPersistentProperties(instance.GetType(), false))
                {
                    var alias = prop.GetCustomAttribute<ScriptAliasAttribute>()?.Alias;
                    if (alias != null) // only show items with ScriptAlias
                    {
                        var propName = prop.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? alias;
                        bool hidden = prop.GetCustomAttribute<PersistentAttribute>().Encrypted;

                        var value = prop.GetValue(instance);
                        if (value is SecureString secure)
                            value = AH.Unprotect(secure);

                        if (hidden && !showHidden)
                            value = "(hidden)";
                        if (value == null)
                            value = "(not specified)";

                        Console.WriteLine(propName + ": " + value);
                    }
                }
            }

            void store()
            {
                var n = parseQualifiedName();
                var type = (from c in ExtensionsManager.GetComponentsByBaseClass<ResourceCredentials>()
                            let a = c.ComponentType.GetCustomAttribute<ScriptAliasAttribute>()
                            where string.Equals(a?.Alias, n.type, StringComparison.OrdinalIgnoreCase) || string.Equals(c.ComponentType.Name, n.type, StringComparison.OrdinalIgnoreCase)
                            orderby string.Equals(a?.Alias, n.type, StringComparison.OrdinalIgnoreCase) descending
                            select c.ComponentType).FirstOrDefault();

                if (type == null)
                    throw new RompException($"Unknown credentials type \"{n.type}\". Are you missing an extension?");

                var credentials = (ResourceCredentials)Activator.CreateInstance(type);

                if (!Console.IsInputRedirected)
                {
                    foreach (var property in Persistence.GetPersistentProperties(type, true))
                    {
                        Again:
                        Console.Write((property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name) + ": ");
                        string value;
                        if (property.GetCustomAttribute<PersistentAttribute>()?.Encrypted == true || property.PropertyType == typeof(SecureString))
                            value = ReadSensitive();
                        else
                            value = Console.ReadLine();

                        if (!string.IsNullOrEmpty(value))
                        {
                            if (property.PropertyType == typeof(string))
                            {
                                property.SetValue(credentials, value);
                            }
                            else if (property.PropertyType == typeof(SecureString))
                            {
                                property.SetValue(credentials, AH.CreateSecureString(value));
                            }
                            else
                            {
                                try
                                {
                                    var convertedValue = Convert.ChangeType(value, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
                                    property.SetValue(credentials, convertedValue);
                                }
                                catch (Exception ex)
                                {
                                    Console.Error.WriteLine("Invalid value: " + ex.Message);
                                    goto Again;
                                }
                            }
                        }
                    }
                }
                else
                {
                    throw new RompException("Credentials must be stored interactively.");
                }

                RompDb.CreateOrUpdateCredentials(n.name, credentials, true);
                Console.WriteLine("Credentials stored.");
            }

            void delete()
            {
                var n = parseQualifiedName();
                RompDb.DeleteCredentials(n.type, n.name);
                Console.WriteLine("Credentials deleted.");
            }

            (string type, string name) parseQualifiedName()
            {
                var qualifiedName = args.PopCommand();
                if (string.IsNullOrEmpty(qualifiedName))
                    throw new RompException("Expected credentials name.");

                var parts = qualifiedName.Split(new[] { "::" }, StringSplitOptions.None);
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                    throw new RompException("Invalid credentials name specification.");

                return (parts[0], parts[1]);
            }
        }
        private static void Config(ArgList args)
        {
            var command = args.PopCommand()?.ToLowerInvariant();
            switch (command)
            {
                case "list":
                    list();
                    break;
                case "export":
                    export();
                    break;
                case "set":
                    set();
                    break;
                default:
                    Console.WriteLine("Usage:");
                    Console.WriteLine("romp config list");
                    Console.WriteLine("romp config export <file-name> [--overwrite] [--all]");
                    Console.WriteLine("romp config set <key> <value> [--machine]");
                    Console.WriteLine("romp config delete <key>");
                    break;
            }

            void list()
            {
                bool showAll = false;

                args.ProcessOptions(
                    o =>
                    {
                        if (o.Key == "all")
                        {
                            showAll = true;
                            return true;
                        }

                        return false;
                    }
                );

                foreach (var p in typeof(RompConfigValues).GetProperties())
                {
                    var name = p.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName;
                    var value = p.GetValue(showAll ? RompConfig.Values : RompConfig.OverriddenValues);
                    if (!string.IsNullOrEmpty(name) && !Attribute.IsDefined(p, typeof(NotCascadedAttribute)) && value != null)
                        Console.WriteLine(name + "=" + p.GetValue(RompConfig.Values));
                }
            }

            void export()
            {
                bool includeAll = false;
                bool overwrite = false;

                args.ProcessOptions(
                    o =>
                    {
                        if (o.Key == "all")
                        {
                            includeAll = true;
                            return true;
                        }
                        else if (o.Key == "overwrite")
                        {
                            overwrite = true;
                            return true;
                        }

                        return false;
                    }
                );

                var fileName = args.PopCommand();
                if (string.IsNullOrEmpty(fileName))
                    throw new RompException("Usage: romp config export <file-name> [--overwrite] [--all]");

                args.ThrowIfAnyRemaining();

                if (!overwrite && File.Exists(fileName))
                    throw new RompException($"File {fileName} already exists. Use --overwrite if overwriting is intentional.");

                using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                using (var writer = new StreamWriter(fileStream, InedoLib.UTF8Encoding))
                {
                    var serializer = JsonSerializer.Create(new JsonSerializerSettings { Formatting = Formatting.Indented });
                    serializer.Serialize(writer, includeAll ? RompConfig.Values : RompConfig.OverriddenValues);
                }

                Console.WriteLine("Configuration written to " + fileName);
            }

            void set()
            {
            }
        }
        private static async Task Packages(ArgList args)
        {
            var command = args.PopCommand()?.ToLowerInvariant();
            switch (command)
            {
                case "list":
                    await list();
                    break;
                default:
                    Console.WriteLine("Usage:");
                    Console.WriteLine("romp packages list");
                    break;
            }

            async Task list()
            {
                using (var registry = PackageRegistry.GetRegistry(RompConfig.UserMode))
                {
                    var packages = await registry.GetInstalledPackagesAsync();
                    if (packages.Count > 0)
                    {
                        Console.WriteLine("Installed packages:");
                        foreach (var p in packages)
                            Console.WriteLine($" {p.Name}");
                    }
                    else
                    {
                        Console.WriteLine("Installed packages: (none)");
                    }
                }
            }
        }
        private static void About(ArgList args)
        {
            Console.WriteLine("Components:");
            var assemblyNames = new[] { "Inedo.SDK", "Inedo.ExecutionEngine", "Inedo.Agents.Client", "Inedo.UPack" };
            foreach (var asmName in assemblyNames)
            {
                var asm = Assembly.Load(asmName);
                var name = asm.GetName();
                var info = FileVersionInfo.GetVersionInfo(asm.Location);
                Console.WriteLine("  " + name.Name + " " + info.FileVersion);
            }

            Console.WriteLine();
            Console.WriteLine("Extensions:");
            ExtensionsManager.WaitForInitialization();
            bool anyExtensions = false;
            foreach (var ext in ExtensionsManager.GetExtensions(false))
            {
                if (ext.LoadResult.Loaded)
                {
                    Console.WriteLine("  " + ext.Name + " " + ext.LoadResult.AssemblyVersion);
                    anyExtensions = true;
                }
            }

            if (!anyExtensions)
                Console.WriteLine("  (no extensions loaded)");
        }

        private static string ReadSensitive()
        {
            ConsoleKeyInfo key;
            var buffer = new StringBuilder();
            while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
            {
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Length--;
                    }
                }
                else if (key.KeyChar > 0)
                {
                    buffer.Append(key.KeyChar);
                }
            }

            Console.WriteLine();
            return buffer.ToString();
        }
        [Conditional("DEBUG")]
        private static void WaitForEnter()
        {
            if (Debugger.IsAttached)
            {
                Console.WriteLine("[DebugMode] Press enter to exit.");
                Console.ReadLine();
            }
        }
    }
}
