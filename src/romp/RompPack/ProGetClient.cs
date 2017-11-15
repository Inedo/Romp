using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;
using Inedo.IO;
using Inedo.Romp.Data;

namespace Inedo.Romp.RompPack
{
    internal sealed class ProGetClient
    {
        private ProGetClient(string endpointUrl) => this.EndpointUrl = endpointUrl.TrimEnd('/') + "/";

        public string EndpointUrl { get; }

        public static ProGetClient GetClient(string name)
        {
            if (name.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || name.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return new ProGetClient(name);

            var source = RompDb.GetPackageSources()
                .FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

            if (source != null)
                return new ProGetClient(source.FeedUrl);

            throw new RompException("Unknown package source: " + name);
        }

        public async Task<Stream> DownloadPackageAsync(string fullName, string version)
        {
            var url = this.EndpointUrl + "download/" + Uri.EscapeUriString(fullName);
            if (!string.IsNullOrEmpty(version))
                url += "/" + Uri.EscapeDataString(version);
            else
                url += "?latest";

            var request = WebRequest.CreateHttp(url);
            request.UseDefaultCredentials = true;
            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                {
                    var tempStream = TemporaryStream.Create(response.ContentLength);
                    try
                    {
                        using (var responseStream = response.GetResponseStream())
                        {
                            await responseStream.CopyToAsync(tempStream).ConfigureAwait(false);
                        }

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
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse res)
                {
                    if (res.ContentType == MediaTypeNames.Text.Plain)
                    {
                        string message;
                        using (var responseStream = res.GetResponseStream())
                        using (var reader = new StreamReader(responseStream))
                        {
                            message = reader.ReadToEnd();
                        }

                        throw new RompException($"Server error: ({(int)res.StatusCode}) {AH.CoalesceString(message, ex.Message)}");
                    }
                }

                throw new RompException("Unable to download package: " + ex.Message);
            }
        }
    }
}
