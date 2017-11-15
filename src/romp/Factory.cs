using System;
using Inedo.Extensibility.Credentials;
using Inedo.Romp.Data;
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
    }
}
