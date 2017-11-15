using System;
using Inedo.Diagnostics;
using Newtonsoft.Json;

namespace Inedo.Romp.Configuration
{
    internal sealed class MessageLevelConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(MessageLevel) || objectType == typeof(MessageLevel?);
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                if (objectType != typeof(MessageLevel?))
                    throw new JsonSerializationException("Cannot convert null value to MessagLevel.");

                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                var enumText = reader.Value.ToString().ToLowerInvariant();
                switch (enumText)
                {
                    case "debug":
                        return MessageLevel.Debug;
                    case "info":
                    case "information":
                        return MessageLevel.Information;
                    case "warn":
                    case "warning":
                        return MessageLevel.Warning;
                    case "error":
                        return MessageLevel.Error;
                }
            }

            throw new JsonSerializationException("Invalid message level value. Value values are: debug, info, warn, or error.");
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            if (value is MessageLevel level)
            {
                switch (level)
                {
                    case MessageLevel.Debug:
                        writer.WriteValue("debug");
                        break;
                    case MessageLevel.Information:
                        writer.WriteValue("info");
                        break;
                    case MessageLevel.Warning:
                        writer.WriteValue("warn");
                        break;
                    case MessageLevel.Error:
                        writer.WriteValue("error");
                        break;
                }
            }

            throw new JsonSerializationException("Invalid message level value.");
        }
    }
}
