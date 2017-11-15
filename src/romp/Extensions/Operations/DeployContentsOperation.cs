using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
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
    [ScriptAlias("Deploy-Contents")]
    [DefaultProperty(nameof(TargetDirectory))]
    public sealed class DeployContentsOperation : ExecuteOperation
    {
        [ScriptAlias("To")]
        public string TargetDirectory { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var sourcePath = PackageInstaller.PackageContentsPath;
            if (string.IsNullOrEmpty(sourcePath))
                throw new ExecutionFailureException(true, "Romp package contents path not set.");
            if (!PathEx.IsPathRooted(sourcePath))
                throw new ExecutionFailureException(true, "Romp package contents path is not absolute.");

            var deployTarget = context.ResolvePath(this.TargetDirectory);

            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false);
            if (await fileOps.DirectoryExistsAsync(sourcePath).ConfigureAwait(false))
                await copyDirectoryAsync(sourcePath, deployTarget).ConfigureAwait(false);

            async Task copyDirectoryAsync(string src, string dest)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                await fileOps.CreateDirectoryAsync(dest).ConfigureAwait(false);

                var items = new Queue<SlimFileSystemInfo>(
                    from i in await fileOps.GetFileSystemInfosAsync(src, MaskingContext.Default).ConfigureAwait(false)
                    orderby i is SlimFileInfo descending
                    select i
                );

                // do it this awkward way to reduce GC pressure if there are lots of files
                SlimFileSystemInfo current;
                while ((current = items.Count > 0 ? items.Dequeue() : null) != null)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();

                    var targetPath = fileOps.CombinePath(dest, current.Name);
                    if (current is SlimFileInfo)
                    {
                        this.LogDebug($"Deploying {targetPath}...");
                        await fileOps.CopyFileAsync(current.FullName, targetPath, true).ConfigureAwait(false);
                    }
                    else
                    {
                        await copyDirectoryAsync(current.FullName, targetPath).ConfigureAwait(false);
                    }
                }
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Deploy package to ",
                    new DirectoryHilite(config[nameof(TargetDirectory)])
                )
            );
        }
    }
}
