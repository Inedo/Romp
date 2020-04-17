using System;
using System.Collections.Generic;
using System.Linq;
using Inedo.Configuration;
using Inedo.Extensibility.RaftRepositories;
using Inedo.Extensibility.UserDirectories;
using Inedo.Romp.Data;
using Inedo.Security;

namespace Inedo.Romp
{
    internal sealed class RompSdkConfig : InedoSdkConfig
    {
        private RompSdkConfig()
        {
        }

        public override string ProductName => "Romp";
        public override Version ProductVersion => typeof(RompSdkConfig).Assembly.GetName().Version;
        public override Type SecuredTaskType => typeof(HedgehogSecuredTask);
        public override string DefaultRaftName => "Default";
        public override string BaseUrl => null;

        public static void Initialize() => Initialize(new RompSdkConfig());
        public override IEnumerable<SDK.CredentialsInfo> GetCredentials() => RompDb.GetCredentials().Select(c => new SDK.CredentialsInfo(c.CredentialType_Name, c.Credential_Name, c.Configuration_Xml, null, null));
        public override IEnumerable<SDK.ServerInfo> GetServers(bool includeInactive) => new[] { new SDK.ServerInfo(1, "localhost") };
        public override UserDirectory CreateUserDirectory(int userDirectoryId) => null;
        public override string GetConfigValue(string configKey) => null;
        public override ITaskChecker GetCurrentTaskChecker() => null;
        public override IUserDirectoryUser GetCurrentUser() => null;
        public override UserDirectory GetCurrentUserDirectory() => null;
        public override IEnumerable<SDK.EnvironmentInfo> GetEnvironments() => Enumerable.Empty<SDK.EnvironmentInfo>();
        public override IEnumerable<SDK.ProjectInfo> GetProjects() => Enumerable.Empty<SDK.ProjectInfo>();
        public override IEnumerable<SDK.ServerRoleInfo> GetServerRoles() => Enumerable.Empty<SDK.ServerRoleInfo>();
        public override IEnumerable<SDK.ServerInfo> GetServersInEnvironment(int environmentId) => Enumerable.Empty<SDK.ServerInfo>();
        public override IEnumerable<SDK.ServerInfo> GetServersInRole(int roleId) => Enumerable.Empty<SDK.ServerInfo>();
        public override RaftRepository CreateRaftRepository(string raftName, OpenRaftOptions options) => Factory.CreateRaftRepository(raftName, options);
        public override IEnumerable<SDK.UserDirectoryInfo> GetUserDirectories() => Enumerable.Empty<SDK.UserDirectoryInfo>();
        public override IEnumerable<SDK.PackageSourceInfo> GetPackageSources() => Enumerable.Empty<SDK.PackageSourceInfo>();
        public override IEnumerable<SDK.ContainerSourceInfo> GetContainerSources() => Enumerable.Empty<SDK.ContainerSourceInfo>();
    }
}
