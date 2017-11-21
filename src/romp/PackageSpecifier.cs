using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.IO;
using Inedo.Romp.Data;
using Inedo.UPack;
using Inedo.UPack.Net;
using Inedo.UPack.Packaging;

namespace Inedo.Romp
{
    internal sealed class PackageSpecifier
    {
        private PackageSpecifier()
        {
        }

        public string FileName { get; private set; }
        public UniversalFeedEndpoint Source { get; private set; }
        public UniversalPackageId PackageId { get; private set; }
        public UniversalPackageVersion PackageVersion { get; private set; }

        public static PackageSpecifier FromArgs(ArgList args)
        {
            var packageName = args.PopCommand();
            if (string.IsNullOrEmpty(packageName))
                return null;

            var inst = new PackageSpecifier();
            args.ProcessOptions(inst.ParseOption);

            if (packageName.EndsWith(".upack", StringComparison.OrdinalIgnoreCase))
            {
                if (inst.Source != null)
                    throw new RompException("--source cannot be specified if <packageName> refers to a file.");
                if (inst.PackageVersion != null)
                    throw new RompException("--version cannot be specified if <packageName> refers to a file.");

                inst.FileName = packageName;
            }
            else if (inst.Source != null)
            {
                try
                {
                    inst.PackageId = UniversalPackageId.Parse(packageName);
                }
                catch (Exception ex)
                {
                    throw new RompException("Invalid package name: " + packageName, ex);
                }
            }
            else
            {
                throw new RompException("Missing --source.");
            }

            return inst;
        }

        public async Task<UniversalPackage> FetchPackageAsync(ArgList args, CancellationToken cancellationToken)
        {
            var stream = await this.FetchPackageStreamAsync(args, cancellationToken);
            try
            {
                return new UniversalPackage(stream);
            }
            catch (InvalidDataException ex)
            {
                throw new RompException("Invalid package file: " + ex.Message, ex);
            }
        }

        private async Task<Stream> FetchPackageStreamAsync(ArgList args, CancellationToken cancellationToken)
        {
            if (this.FileName != null)
            {
                if (File.Exists(this.FileName))
                {
                    try
                    {
                        return new FileStream(this.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                    catch (FileNotFoundException)
                    {
                    }
                    catch (DirectoryNotFoundException)
                    {
                    }
                }

                throw new RompException("File not found: " + this.FileName);
            }

            if (this.Source != null)
            {
                var client = new UniversalFeedClient(this.Source);
                RemoteUniversalPackageVersion package;

                if (this.PackageVersion == null)
                {
                    package = (await client.ListPackageVersionsAsync(this.PackageId, false, null, cancellationToken))
                        .OrderByDescending(v => v.Version)
                        .FirstOrDefault();
                    if (package == null)
                        throw new RompException($"Package {this.PackageId} not found at the specified source.");
                }
                else
                {
                    package = await client.GetPackageVersionAsync(this.PackageId, this.PackageVersion, false, cancellationToken);
                    if (package == null)
                        throw new RompException($"Package {this.PackageId} version {this.PackageVersion} not found at the specified source.");
                }

                using (var remoteStream = await client.GetPackageStreamAsync(package.FullName, package.Version, cancellationToken))
                {
                    if (remoteStream == null)
                        throw new RompException($"Package {package.FullName} version {package.Version} not found at the specified source.");

                    var tempStream = TemporaryStream.Create(package.Size);
                    try
                    {
                        await remoteStream.CopyToAsync(tempStream, 81920, cancellationToken);
                        tempStream.Position = 0;
                        return tempStream;
                    }
                    catch
                    {
                        tempStream.Dispose();
                        throw;
                    }
                }
            }

            throw new InvalidOperationException("Invalid package specification.");
        }

        public override string ToString()
        {
            if (this.FileName != null)
                return this.FileName;

            if (this.Source != null && this.PackageId != null)
            {
                var id = this.PackageId.ToString();
                if (this.PackageVersion != null)
                    id += " " + this.PackageVersion;

                return id + " from " + this.Source;
            }

            return string.Empty;
        }

        private bool ParseOption(ArgOption o)
        {
            if (string.Equals("source", o.Key, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(o.Value))
                    throw new RompException("Expected source name or URL after --source=");

                if (o.Value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || o.Value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    this.Source = new UniversalFeedEndpoint(o.Value, true);
                }
                else
                {
                    var source = RompDb.GetPackageSources()
                        .FirstOrDefault(s => string.Equals(s.Name, o.Value, StringComparison.OrdinalIgnoreCase));

                    if (source == null)
                        throw new RompException($"Package source \"{o.Value}\" not found.");

                    if (string.IsNullOrEmpty(source.UserName) || source.Password == null)
                        this.Source = new UniversalFeedEndpoint(source.FeedUrl, true);
                    else
                        this.Source = new UniversalFeedEndpoint(new Uri(source.FeedUrl), source.UserName, source.Password);
                }

                return true;
            }
            else if (string.Equals("version", o.Key, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(o.Value))
                    throw new RompException("Expected package version after --version=");

                try
                {
                    this.PackageVersion = UniversalPackageVersion.Parse(o.Value);
                }
                catch (Exception ex)
                {
                    throw new RompException("Invalid version: " + o.Value, ex);
                }

                return true;
            }

            return false;
        }
    }
}
