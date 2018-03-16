using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.IO;
using Inedo.Romp.RompPack;

namespace Inedo.Romp.Extensions.Operations
{
    [ScriptNamespace("Romp")]
    [ScriptAlias("Delete-Contents")]
    [DefaultProperty(nameof(TargetDirectory))]
    public sealed class DeleteContentsOperation : ExecuteOperation
    {
        [ScriptAlias("From")]
        public string TargetDirectory { get; set; }
    
        public override Task ExecuteAsync(IOperationExecutionContext context)
        {
            var sourcePath = PackageInstaller.PackageContentsPath;
            if (string.IsNullOrEmpty(sourcePath))
                throw new ExecutionFailureException(true, "Romp package contents path not set.");
            if (!PathEx.IsPathRooted(sourcePath))
                throw new ExecutionFailureException(true, "Romp package contents path is not absolute.");

            var deployTarget = context.ResolvePath(this.TargetDirectory);

            foreach (var sourceFileName in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                if (sourceFileName.Length <= sourcePath.Length)
                    continue;

                var relativePath = sourceFileName.Substring(sourcePath.Length).TrimStart('\\', '/');
                var targetPath = PathEx.Combine(deployTarget, relativePath);
                try
                {
                    if (File.Exists(targetPath))
                    {
                        this.LogDebug($"Deleting {targetPath}...");
                        File.Delete(targetPath);
                    }
                }
                catch (Exception ex) when (!(ex is FileNotFoundException) && !(ex is DirectoryNotFoundException))
                {
                    this.LogError($"Unable to delete {targetPath}: {ex.Message}");
                }
            }

            var remainingDirs = Directory.EnumerateDirectories(deployTarget, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Count(c => c == '\\' || c == '/'))
                .Concat(new[] { deployTarget });

            foreach (var dir in remainingDirs)
            {
                try
                {
                    this.LogDebug($"Removing directory {dir}...");
                    Directory.Delete(dir);
                }
                catch
                {
                    this.LogDebug($"Directory {dir} is not empty; will not remove.");
                }
            }

            return Complete;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Delete package contents from ",
                    new DirectoryHilite(config[nameof(TargetDirectory)])
                )
            );
        }
    }
}
