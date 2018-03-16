using System;
using System.IO;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.RaftRepositories;
using Inedo.Romp.Data;
using Inedo.Romp.RompPack;
using Inedo.Serialization;

namespace Inedo.Romp
{
    internal static class Factory
    {
        public static ResourceCredentials CreateResourceCredentials(Tables.Credentials_Extended c)
        {
            if (c == null)
                throw new ArgumentNullException(nameof(c));

            return (ResourceCredentials)Persistence.DeserializeFromPersistedObjectXml(c.Configuration_Xml);
        }

        public static RaftRepository CreateRaftRepository(string name, OpenRaftOptions options)
        {
            var raftDirectory = Path.Combine(PackageInstaller.PackageRootPath, "rafts", name);
            if (raftDirectory != null)
            {
                return new DirectoryRaftRepository
                {
                    RepositoryPath = raftDirectory,
                    RaftName = name,
                    OpenOptions = options,
                    ReadOnly = true
                };
            }

            return null;
        }
    }
}
