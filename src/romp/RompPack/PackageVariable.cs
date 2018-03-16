using System.Linq;
using Inedo.ExecutionEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inedo.Romp.RompPack
{
    internal sealed class PackageVariable
    {
        public PackageVariable(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                this.Required = true;
            }
            else if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                var value = obj.Property("value")?.Value;
                var type = obj.Property("type")?.Value;
                var required = obj.Property("required")?.Value;
                var sensitive = obj.Property("sensitive")?.Value;
                var description = obj.Property("description")?.Value;

                // if any of these are defined, treat this as a full definition
                if ((value ?? type ?? required ?? sensitive ?? description) != null)
                {
                    if (value != null && value.Type != JTokenType.Null)
                        this.Value = Convert(value);

                    this.Description = description?.Value<string>();
                    this.Required = required?.Value<bool>() ?? false;
                    this.Sensitive = sensitive?.Value<bool>() ?? false;
                }
                else
                {
                    this.Value = Convert(token);
                }
            }
            else
            {
                this.Value = Convert(token);
            }
        }
        private PackageVariable(PackageVariableType type, bool required, string description)
        {
            this.Type = type;
            this.Required = required;
            this.Description = description;
        }

        public RuntimeValue? Value { get; }
        public string Description { get; }
        public bool Required { get; }
        public bool Sensitive { get; }
        public PackageVariableType Type { get; }

        public static PackageVariable DefaultTargetDirectoryVariable => new PackageVariable(PackageVariableType.Text, true, "Directory to install the package contents.");

        private static RuntimeValue Convert(JToken token)
        {
            if (token.Type == JTokenType.Boolean)
                return new RuntimeValue(token.Value<bool>());
            if (token.Type == JTokenType.Array)
                return new RuntimeValue(((JArray)token).Select(Convert).ToList());
            if (token.Type == JTokenType.Null)
                return new RuntimeValue(string.Empty);

            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                return new RuntimeValue(obj.Properties().ToDictionary(p => p.Name, p => Convert(p.Value)));
            }

            return token.Value<string>();
        }

        private sealed class FullVariableJson
        {
            [JsonProperty("value")]
            public object Value { get; set; }
            [JsonProperty("description")]
            public string Description { get; set; }
            [JsonProperty("required")]
            public bool Required { get; set; }
            [JsonProperty("sensitive")]
            public bool Sensitive { get; set; }
            [JsonProperty("type")]
            public PackageVariableType? Type { get; set; }
        }
    }

    internal enum PackageVariableType
    {
        Text,
        Boolean,
        List,
        Map
    }
}
