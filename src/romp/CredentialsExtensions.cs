using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Romp.Data;

namespace Inedo.Romp
{
    internal static class CredentialsExtensions
    {
        public static TCredential TryGetCredentials<TCredential>(this IHasCredentials<TCredential> obj)
            where TCredential : ResourceCredentials, new()
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            var name = CredentialName.TryParse(obj.CredentialName);
            if (name == null)
                return null;

            var credentialInfo = RompDb.GetCredentialsByName(name.TypeName, name.Name);
            if (credentialInfo == null)
                return null;

            return (TCredential)Factory.CreateResourceCredentials(credentialInfo);
        }

        public static ResourceCredentials TryGetCredentials(this IHasCredentials obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            var name = CredentialName.TryParse(obj.CredentialName);
            if (name == null)
                return null;

            var credentialInfo = RompDb.GetCredentialsByName(name.TypeName, name.Name);
            if (credentialInfo == null)
                return null;

            return Factory.CreateResourceCredentials(credentialInfo);
        }

        public static void SetValues(this IHasCredentials target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var credentials = target.TryGetCredentials();
            if (credentials == null)
                return;

            var mappedProperties = CredentialMappedProperty.GetCredentialMappedProperties(target.GetType());

            foreach (var mappedProperty in mappedProperties)
            {
                if (mappedProperty.Property.GetValue(target) != null)
                    continue;

                mappedProperty.SetValue(target, credentials);
            }

            target.CredentialName = null;
        }

        private sealed class CredentialMappedProperty
        {
            public PropertyInfo Property { get; }
            public string MappedCredentialPropertyName { get; }

            private CredentialMappedProperty(PropertyInfo property, string mappedCredentialPropertyName)
            {
                this.Property = property;
                this.MappedCredentialPropertyName = mappedCredentialPropertyName;
            }

            public static IEnumerable<CredentialMappedProperty> GetCredentialMappedProperties(Type type)
            {
                if (type == null)
                    throw new ArgumentNullException(nameof(type));

                var props = from t in AhReflection.GetBaseTypes(type)
                            from p in t.GetProperties(BindingFlags.Instance | BindingFlags.SetProperty | BindingFlags.Public)
                            where p.CanRead && p.CanWrite
                            let a = p.GetCustomAttribute<MappedCredentialAttribute>()
                            where a != null
                            select new CredentialMappedProperty(p, a.PropertyName);
                return props;
            }
            public void SetValue(object target, ResourceCredentials credentials)
            {
                var credentialPropertyValue = this.GetCredentialPropertyValue(credentials);
                this.Property.SetValue(target, ConvertValue(credentialPropertyValue, this.Property.PropertyType));
            }

            private object GetCredentialPropertyValue(ResourceCredentials credentials)
            {
                var prop = credentials.GetType().GetProperty(this.MappedCredentialPropertyName);
                if (prop == null)
                    return null;

                return prop.GetValue(credentials);
            }
            private static object ConvertValue(object value, Type targetType)
            {
                if (value == null)
                    return null;

                var sourceType = value.GetType();
                if (targetType.IsAssignableFrom(sourceType))
                    return value;

                if (targetType == typeof(string))
                {
                    if (value is SecureString secure)
                        return AH.Unprotect(secure);
                    else
                        return value.ToString();
                }
                else if (targetType == typeof(SecureString))
                {
                    return AH.CreateSecureString(value.ToString());
                }
                else
                {
                    throw new NotSupportedException($"Type {sourceType.Name} cannot be converted to {targetType.Name} in resource credential target.");
                }
            }
        }
    }
}
