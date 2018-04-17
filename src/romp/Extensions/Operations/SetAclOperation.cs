using System;
using System.IO;
using System.Security.AccessControl;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Romp.Extensions.Operations
{
    [Serializable]
    [ScriptAlias("Set-ACL")]
    [ScriptNamespace("InedoInternal", PreferUnqualified = false)]
    public sealed class SetAclOperation : RemoteExecuteOperation
    {
        [Required]
        [ScriptAlias("User")]
        public string UserName { get; set; }
        [Required]
        [ScriptAlias("Path")]
        public string Path { get; set; }

        protected override Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            Directory.CreateDirectory(this.Path);

            try
            {
                this.LogInformation($"Setting ACL on {this.Path} for {this.UserName}...");
                var acl = Directory.GetAccessControl(this.Path);
                acl.AddAccessRule(new FileSystemAccessRule(this.UserName, FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.Modify | FileSystemRights.Synchronize, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                Directory.SetAccessControl(this.Path, acl);
            }
            catch (Exception ex)
            {
                this.LogWarning("Unable to set ACL: " + ex.ToString());
            }

            return Complete;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Grant access to ",
                    new DirectoryHilite(config[nameof(this.Path)])
                ),
                new RichDescription(
                    "for ",
                    new Hilite(config[nameof(this.UserName)])
                )
            );
        }
    }
}
