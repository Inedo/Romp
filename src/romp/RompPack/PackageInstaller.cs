using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Parser;
using Inedo.Romp.Data;
using Inedo.Romp.RompExecutionEngine;

namespace Inedo.Romp.RompPack
{
    internal sealed class PackageInstaller
    {
        public Stream SourceStream { get; set; }
        public bool Simulate { get; set; }
        public bool Force { get; set; }

        public static string PackageContentsPath { get; private set; }

        public async Task RunAsync()
        {
            await ExtensionsManager.WaitForInitializationAsync().ConfigureAwait(false);

            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempPath);
                Console.WriteLine("Extracting package...");
                using (var zip = new ZipArchive(this.SourceStream, ZipArchiveMode.Read))
                {
                    zip.ExtractToDirectory(tempPath);
                }

                var installScriptFileName = Path.Combine(tempPath, "install.otter");
                var installScript = Compile(installScriptFileName);
                if (installScript == null)
                    throw new RompException("Unable to compile install.otter.");

                PackageContentsPath = Path.Combine(tempPath, "package");
                await this.RunPlan(installScript);
            }
            finally
            {
                Console.WriteLine("Cleaning up...");
                try { Directory.Delete(tempPath, true); }
                catch { }
            }
        }

        private async Task RunPlan(ScopedStatementBlock script)
        {
            RompRaftFactory.Initialize();

            if (this.Simulate)
                Console.WriteLine("Running as simulation");

            Console.WriteLine();

            var executer = new RompExecutionEnvironment(script, this.Simulate);
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
                    RompConsoleMessenger.WriteDirect($"Job #{executer.ExecutionId} completed successfully.", ConsoleColor.White);
                else if (exec.StatusCode == Domains.ExecutionStatus.Warning)
                    RompConsoleMessenger.WriteDirect($"Job #{executer.ExecutionId} completed with warnings.", ConsoleColor.Yellow);
                else
                    RompConsoleMessenger.WriteDirect($"Job #{executer.ExecutionId} encountered an error.", ConsoleColor.Red);

                Console.WriteLine();
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
