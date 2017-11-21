using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Inedo.IO;
using Inedo.Romp.Data;
using Inedo.UPack;
using Inedo.UPack.Net;

namespace Inedo.Romp.RompPack
{
    internal static class ProGetClient
    {
        public static UniversalFeedClient GetClient(string name)
        {
            if (name.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || name.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return new UniversalFeedClient(name);

            var source = RompDb.GetPackageSources()
                .FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

            if (source != null)
            {
                var endpoint = (string.IsNullOrEmpty(source.UserName) || source.Password == null)
                    ? new UniversalFeedEndpoint(source.FeedUrl, true)
                    : new UniversalFeedEndpoint(new Uri(source.FeedUrl), source.UserName, source.Password);

                return new UniversalFeedClient(endpoint);                   
            }

            throw new RompException("Unknown package source: " + name);
        }

        public static async Task<Stream> DownloadPackageAsync(this UniversalFeedClient client, string fullName, string version, CancellationToken cancellationToken = default)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrEmpty(fullName))
                throw new ArgumentNullException(nameof(fullName));

            UniversalPackageId packageId;
            try
            {
                packageId = UniversalPackageId.Parse(fullName);
            }
            catch (Exception ex)
            {
                throw new RompException("Invalid package name: " + fullName, ex);
            }

            UniversalPackageVersion v = null;
            if (!string.IsNullOrEmpty(version))
            {
                try
                {
                    v = UniversalPackageVersion.TryParse(version);
                }
                catch (Exception ex)
                {
                    throw new RompException("Invalid package version: " + version, ex);
                }
            }

            var packageInfo = await client.GetPackageVersionAsync(packageId, v, false, cancellationToken);
            if (packageInfo == null)
                throw new RompException($"Package {fullName} {version} not found.");

            using (var packageStream = await client.GetPackageStreamAsync(packageInfo.FullName, packageInfo.Version, cancellationToken))
            {
                var tempStream = TemporaryStream.Create(packageInfo.Size);
                try
                {
                    await packageStream.CopyToAsync(tempStream, 81920, cancellationToken);
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

        //public async Task<Stream> DownloadPackageAsync(string fullName, string version)
        //{
        //    var url = this.EndpointUrl + "download/" + Uri.EscapeUriString(fullName);
        //    if (!string.IsNullOrEmpty(version))
        //        url += "/" + Uri.EscapeDataString(version);
        //    else
        //        url += "?latest";

        //    var request = WebRequest.CreateHttp(url);
        //    request.UseDefaultCredentials = true;
        //    try
        //    {
        //        using (var response = await request.GetResponseAsync().ConfigureAwait(false))
        //        {
        //            var tempStream = TemporaryStream.Create(response.ContentLength);
        //            try
        //            {
        //                using (var responseStream = response.GetResponseStream())
        //                {
        //                    await responseStream.CopyToAsync(tempStream).ConfigureAwait(false);
        //                }

        //                tempStream.Position = 0;
        //                return tempStream;
        //            }
        //            catch
        //            {
        //                tempStream.Dispose();
        //                throw;
        //            }
        //        }
        //    }
        //    catch (WebException ex)
        //    {
        //        if (ex.Response is HttpWebResponse res)
        //        {
        //            if (res.ContentType == MediaTypeNames.Text.Plain)
        //            {
        //                string message;
        //                using (var responseStream = res.GetResponseStream())
        //                using (var reader = new StreamReader(responseStream))
        //                {
        //                    message = reader.ReadToEnd();
        //                }

        //                throw new RompException($"Server error: ({(int)res.StatusCode}) {AH.CoalesceString(message, ex.Message)}");
        //            }
        //        }

        //        throw new RompException("Unable to download package: " + ex.Message);
        //    }
        //}
    }
}
