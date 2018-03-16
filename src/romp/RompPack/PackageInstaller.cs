using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Parser;
using Inedo.Romp.Data;
using Inedo.Romp.RompExecutionEngine;
using Inedo.UPack;
using Inedo.UPack.Packaging;

namespace Inedo.Romp.RompPack
{
    internal static class PackageInstaller
    {
        // these properties contain the global state available to some operations and variables
        public static string TargetDirectory { get; set; }
        public static string PackageRootPath { get; private set; }
        public static string PackageContentsPath => Path.Combine(PackageRootPath, "package");
        public static UniversalPackageId PackageId { get; private set; }
        public static UniversalPackageVersion PackageVersion { get; private set; }

        public static async Task RunAsync(UniversalPackage package, string script, bool simulate)
        {
            PackageId = new UniversalPackageId(package.Group, package.Name);
            PackageVersion = package.Version;

            await ExtensionsManager.WaitForInitializationAsync().ConfigureAwait(false);

            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempPath);
                Console.WriteLine("Extracting package...");
                await package.ExtractAllItemsAsync(tempPath, default);

                var installScriptFileName = Path.Combine(tempPath, script);
                var installScript = Compile(installScriptFileName);
                if (installScript == null)
                    throw new RompException($"Unable to compile {script}.");

                PackageRootPath = tempPath;
                await RunPlanAsync(installScript, simulate);
            }
            finally
            {
                Console.WriteLine("Cleaning up...");
                try { Directory.Delete(tempPath, true); }
                catch { }
            }
        }

        private static async Task RunPlanAsync(ScopedStatementBlock script, bool simulate)
        {
            if (simulate)
                Console.WriteLine("Running as simulation");

            Console.WriteLine();

            var executer = new RompExecutionEnvironment(script, simulate);
            if (!Console.IsOutputRedirected)
            {
                RompConsoleMessenger.ShowProgress = true;
                using (var done = new CancellationTokenSource())
                {
                    Task consoleTask = null;
                    try
                    {
                        consoleTask = Task.Run(() => RompConsoleMessenger.MonitorStatus(executer, done.Token));
                        await executer.ExecuteAsync();
                    }
                    finally
                    {
                        done.Cancel();
                        try
                        {
                            if (consoleTask != null)
                                await consoleTask;
                        }
                        catch
                        {
                        }
                    }
                }
            }
            else
            {
                RompConsoleMessenger.ShowProgress = false;
                await executer.ExecuteAsync();
            }

            var exec = RompDb.GetExecutions()
                .FirstOrDefault(e => e.ExecutionId == executer.ExecutionId);

            if (exec != null)
            {
                var logs = RompDb.GetExecutionLogs(exec.ExecutionId);
                foreach (var log in logs)
                    log.WriteErrors(Console.Out);

                if (exec.StatusCode == Domains.ExecutionStatus.Normal)
                {
                    RompConsoleMessenger.WriteDirect($"Job #{executer.ExecutionId} completed successfully.", ConsoleColor.White);
                }
                else if (exec.StatusCode == Domains.ExecutionStatus.Warning)
                {
                    RompConsoleMessenger.WriteDirect($"Job #{executer.ExecutionId} completed with warnings.", ConsoleColor.Yellow);
                }
                else
                {
                    RompConsoleMessenger.WriteDirect($"Job #{executer.ExecutionId} encountered an error.", ConsoleColor.Red);
                    throw new RompException("Job execution failed.");
                }

                Console.WriteLine();
            }
            else
            {
                throw new RompException("Execution could not be created.");
            }
        }
        private static ScopedStatementBlock Compile(string planFile)
        {
            var results = Compiler.Compile(planFile);

            foreach (var error in results.Errors)
            {
                var message = $"{error.Message} (at line #{error.LineNumber})";
                if (error.Level == ScriptErrorLevel.Warning)
                    Logger.Warning(message);
                else
                    Logger.Error(message);
            }

            return results.Script;
        }
    }
}
