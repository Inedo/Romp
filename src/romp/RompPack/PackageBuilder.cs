using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;

namespace Inedo.Romp.RompPack
{
    internal static class PackageBuilder
    {
        public static void BuildPackage(string sourcePath, string packageFileName, bool force, bool overwrite)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentNullException(nameof(sourcePath));

            Console.WriteLine($"Building package from {sourcePath}...");
            Console.WriteLine("Looking for upack.json...");

            var upackPath = Path.Combine(sourcePath, "upack.json");
            if (!File.Exists(upackPath))
                throw new RompException("upack.json not found in specified source directory.");

            UpackMetadata upackInfo;

            using (var reader = new JsonTextReader(File.OpenText(upackPath)))
            {
                try
                {
                    upackInfo = new JsonSerializer().Deserialize<UpackMetadata>(reader);
                }
                catch (Exception ex)
                {
                    throw new RompException("Invalid upack.json file: JSON syntax error.", ex);
                }
            }

            if (string.IsNullOrWhiteSpace(upackInfo.Name))
                throw new RompException("Invalid upack.json file: \"name\" property is missing or invalid.");
            if (string.IsNullOrWhiteSpace(upackInfo.Version))
                throw new RompException("Invalid upack.json file: \"version\" property is missing or invalid.");

            Console.WriteLine($"upack.json loaded (group: {upackInfo.Group}, name: {upackInfo.Name}, version: {upackInfo.Version})");

            if (string.IsNullOrEmpty(packageFileName))
                packageFileName = upackInfo.Name + "-" + upackInfo.Version + ".upack";

            if (!overwrite && File.Exists(packageFileName))
                throw new RompException($"File {packageFileName} already exists. Use the --overwrite flag if you mean to overwrite it.");

            Console.WriteLine($"Creating {packageFileName} package...");
            using (var zipArchiveFile = new ZipArchive(File.Create(packageFileName), ZipArchiveMode.Create))
            {
                foreach (var sourceEntry in Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                {
                    var entry = createZipEntry(sourceEntry);
                    using (var sourceStream = new FileStream(sourceEntry, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
                    using (var targetStream = entry.Open())
                    {
                        sourceStream.CopyTo(targetStream);
                    }
                }

                ZipArchiveEntry createZipEntry(string fileName)
                {
                    var name = fileName.Substring(sourcePath.Length).TrimStart('/', '\\').Replace('\\', '/');
                    var entry = zipArchiveFile.CreateEntry(name, CompressionLevel.Optimal);
                    entry.LastWriteTime = File.GetLastWriteTime(fileName);
                    return entry;
                }
            }

            Console.WriteLine($"Package {packageFileName} created.");
        }

        private sealed class UpackMetadata
        {
            [JsonProperty("group")]
            public string Group { get; set; }
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("version")]
            public string Version { get; set; }
        }
    }
}
