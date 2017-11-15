using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Inedo.Romp.RompPack
{
    internal sealed class PackageRegistry : IDisposable
    {
        private bool disposed;

        private PackageRegistry(string registryRoot) => this.RegistryRoot = registryRoot;

        public string RegistryRoot { get; }
        public string LockToken { get; private set; }

        public static PackageRegistry GetRegistry(bool openUserRegistry)
        {
            var root = openUserRegistry ? GetCurrentUserRegistryRoot() : GetMachineRegistryRoot();
            return new PackageRegistry(root);
        }
        public static string GetMachineRegistryRoot() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "upack");
        public static string GetCurrentUserRegistryRoot() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".upack");

        public Task LockAsync(CancellationToken cancellationToken) => this.LockRegistryAsync(cancellationToken);
        public Task UnlockAsync()
        {
            this.UnlockRegistry();
            return InedoLib.NullTask;
        }
        public IList<RegisteredPackage> GetInstalledPackages() => GetInstalledPackages(this.RegistryRoot);
        public void RegisterPackage(RegisteredPackage package, CancellationToken cancellationToken)
        {
            var packages = GetInstalledPackages(this.RegistryRoot);

            packages.RemoveAll(p => RegisteredPackage.NameAndGroupEquals(p, package));
            packages.Add(package);

            WriteInstalledPackages(this.RegistryRoot, packages);
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                if (this.LockToken != null)
                {
                    try
                    {
                        this.UnlockRegistry();
                    }
                    catch
                    {
                    }
                }

                this.disposed = true;
            }
        }

        private async Task LockRegistryAsync(CancellationToken cancellationToken)
        {
            var fileName = Path.Combine(this.RegistryRoot, ".lock");

            var lockDescription = "Locked by Romp";
            var lockToken = Guid.NewGuid().ToString();

            TryAgain:
            var fileInfo = getFileInfo();
            while (fileInfo != null && DateTime.UtcNow - fileInfo.LastWriteTimeUtc <= new TimeSpan(0, 0, 10))
            {
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                fileInfo = getFileInfo();
            }

            // ensure registry root exists
            Directory.CreateDirectory(this.RegistryRoot);

            try
            {
                // write out the lock info
                using (var lockStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                using (var writer = new StreamWriter(lockStream, InedoLib.UTF8Encoding))
                {
                    writer.WriteLine(lockDescription);
                    writer.WriteLine(lockToken.ToString());
                }

                // verify that we acquired the lock
                using (var lockStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                using (var reader = new StreamReader(lockStream, InedoLib.UTF8Encoding))
                {
                    if (reader.ReadLine() != lockDescription)
                        goto TryAgain;

                    if (reader.ReadLine() != lockToken)
                        goto TryAgain;
                }
            }
            catch (IOException)
            {
                // file may be in use by other process
                goto TryAgain;
            }

            // at this point, lock is acquired provided everyone is following the rules
            this.LockToken = lockToken;

            FileInfo getFileInfo()
            {
                try
                {
                    var info = new FileInfo(fileName);
                    if (!info.Exists)
                        return null;
                    return info;
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
                catch (DirectoryNotFoundException)
                {
                    return null;
                }
            }
        }
        private void UnlockRegistry()
        {
            if (this.LockToken == null)
                return;

            var fileName = Path.Combine(this.RegistryRoot, ".lock");
            if (!File.Exists(fileName))
                return;

            string token;
            using (var lockStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(lockStream, InedoLib.UTF8Encoding))
            {
                reader.ReadLine();
                token = reader.ReadLine();
            }

            if (token == this.LockToken)
                File.Delete(fileName);

            this.LockToken = null;
        }
        private static List<RegisteredPackage> GetInstalledPackages(string registryRoot)
        {
            var fileName = Path.Combine(registryRoot, "installedPackages.json");

            if (!File.Exists(fileName))
                return new List<RegisteredPackage>();

            using (var configStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (var streamReader = new StreamReader(configStream, InedoLib.UTF8Encoding))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                return (new JsonSerializer().Deserialize<RegisteredPackage[]>(jsonReader) ?? new RegisteredPackage[0]).ToList();
            }
        }
        private static void WriteInstalledPackages(string registryRoot, IEnumerable<RegisteredPackage> packages)
        {
            Directory.CreateDirectory(registryRoot);
            var fileName = Path.Combine(registryRoot, "installedPackages.json");

            using (var configStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            using (var streamWriter = new StreamWriter(configStream, InedoLib.UTF8Encoding))
            using (var jsonWriter = new JsonTextWriter(streamWriter))
            {
                new JsonSerializer { Formatting = Formatting.Indented }.Serialize(jsonWriter, packages.ToArray());
            }
        }
    }
}
