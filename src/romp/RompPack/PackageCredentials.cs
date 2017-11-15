using System.Collections.Generic;
using Inedo.ExecutionEngine;
using Newtonsoft.Json.Linq;

namespace Inedo.Romp.RompPack
{
    internal sealed class PackageCredentials
    {
        public PackageCredentials(JObject obj)
        {
            this.Type = (string)obj.Property("type");
            this.Name = (string)obj.Property("name");
            this.Description = (string)obj.Property("description");
            this.Restricted = obj.Property("restricted")?.Value<bool>() ?? false;
            this.Defaults = new Dictionary<string, RuntimeValue>();
        }

        public string Type { get; }
        public string Name { get; }
        public string FullName => this.Type + "::" + this.Name;
        public string Description { get; }
        public bool Restricted { get; }
        public IReadOnlyDictionary<string, RuntimeValue> Defaults { get; }
    }
}
