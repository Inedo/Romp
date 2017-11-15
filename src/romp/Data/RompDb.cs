using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Romp.Configuration;
using Inedo.Serialization;

namespace Inedo.Romp.Data
{
    internal static class RompDb
    {
        private static readonly Lazy<string> getConnectionString = new Lazy<string>(() => new SQLiteConnectionStringBuilder { DataSource = RompConfig.DataFilePath, ForeignKeys = true }.ToString(), LazyThreadSafetyMode.PublicationOnly);
        private static readonly Lazy<string> createLogScopeSql = new Lazy<string>(() => GetScript(nameof(CreateLogScope)), LazyThreadSafetyMode.PublicationOnly);
        private static readonly Lazy<string> completeLogScopeSql = new Lazy<string>(() => GetScript(nameof(CompleteLogScope)), LazyThreadSafetyMode.PublicationOnly);
        private static readonly Lazy<string> writeLogMessageSql = new Lazy<string>(() => GetScript(nameof(WriteLogMessage)), LazyThreadSafetyMode.PublicationOnly);

        private static string ConnectionString => getConnectionString.Value;

        public static void Initialize()
        {
            if (!File.Exists(RompConfig.DataFilePath))
            {
                Directory.CreateDirectory(RompConfig.ConfigDataPath);

                var str = new SQLiteConnectionStringBuilder
                {
                    DataSource = RompConfig.DataFilePath,
                    FailIfMissing = false,
                    ForeignKeys = true
                }.ToString();

                using (var conn = new SQLiteConnection(str))
                {
                    conn.Open();

                    using (var cmd = new SQLiteCommand(GetScript(), conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }
        public static int CreateExecution(DateTime startDate, string statusCode, string runStateCode, bool simulation)
        {
            if (!RompConfig.StoreLogs)
                return 1;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(GetScript(), conn))
                {
                    cmd.Parameters.AddWithValue("@Start_Date", startDate.Ticks);
                    cmd.Parameters.AddWithValue("@ExecutionStatus_Code", statusCode);
                    cmd.Parameters.AddWithValue("@ExecutionRunState_Code", runStateCode);
                    cmd.Parameters.AddWithValue("@Simulation_Indicator", simulation ? 1 : 0);

                    return checked((int)(long)cmd.ExecuteScalar());
                }
            }
        }
        public static void CompleteExecution(int executionId, DateTime endDate, string statusCode)
        {
            if (!RompConfig.StoreLogs)
                return;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(GetScript(), conn))
                {
                    cmd.Parameters.AddWithValue("@Execution_Id", executionId);
                    cmd.Parameters.AddWithValue("@End_Date", endDate.Ticks);
                    cmd.Parameters.AddWithValue("@ExecutionStatus_Code", statusCode);

                    cmd.ExecuteNonQuery();
                }
            }
        }
        public static void DeleteExecution(int executionId)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(GetScript(), conn))
                {
                    cmd.Parameters.AddWithValue("@Execution_Id", executionId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public static int CreateLogScope(int executionId, int? parentScopeSequence, string scopeName, DateTime startDate)
        {
            if (!RompConfig.StoreLogs)
                return 0;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(createLogScopeSql.Value, conn))
                {
                    cmd.Parameters.AddWithValue("@Execution_Id", executionId);
                    cmd.Parameters.AddWithValue("@Parent_Scope_Sequence", parentScopeSequence);
                    cmd.Parameters.AddWithValue("@Scope_Name", scopeName);
                    cmd.Parameters.AddWithValue("@Start_Date", startDate.Ticks);

                    return checked((int)(long)cmd.ExecuteScalar());
                }
            }
        }
        public static void CompleteLogScope(int executionId, int scopeSequence, DateTime endDate)
        {
            if (!RompConfig.StoreLogs)
                return;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(completeLogScopeSql.Value, conn))
                {
                    cmd.Parameters.AddWithValue("@Execution_Id", executionId);
                    cmd.Parameters.AddWithValue("@Scope_Sequence", scopeSequence);
                    cmd.Parameters.AddWithValue("@End_Date", endDate.Ticks);

                    cmd.ExecuteNonQuery();
                }
            }

        }
        public static void WriteLogMessage(int executionId, int scopeSequence, int level, string message, DateTime date)
        {
            if (!RompConfig.StoreLogs)
                return;

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(writeLogMessageSql.Value, conn))
                {
                    cmd.Parameters.AddWithValue("@Execution_Id", executionId);
                    cmd.Parameters.AddWithValue("@Scope_Sequence", scopeSequence);
                    cmd.Parameters.AddWithValue("@LogEntry_Level", level);
                    cmd.Parameters.AddWithValue("@LogEntry_Text", message);
                    cmd.Parameters.AddWithValue("@LogEntry_Date", date.Ticks);

                    cmd.ExecuteNonQuery();
                }
            }
        }
        public static IEnumerable<ExecutionData> GetExecutions()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(GetScript(), conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return new ExecutionData(reader);
                    }
                }
            }
        }
        public static IList<ScopedExecutionLog> GetExecutionLogs(int executionId)
        {
            var scopes = new List<ScopedExecutionLog>();
            var entries = new List<LogEntry>();

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(GetScript(), conn))
                {
                    cmd.Parameters.AddWithValue("@Execution_Id", executionId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            scopes.Add(new ScopedExecutionLog(reader));
                        }

                        reader.NextResult();

                        while (reader.Read())
                        {
                            entries.Add(new LogEntry(reader));
                        }
                    }
                }
            }

            return ScopedExecutionLog.Build(scopes, entries);
        }
        public static Tables.Credentials_Extended GetCredentialsByName(string typeName, string name)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(GetScript(), conn))
                {
                    cmd.Parameters.AddWithValue("@CredentialType_Name", typeName);
                    cmd.Parameters.AddWithValue("@Credential_Name", name);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            return ReadCredentials(reader);
                    }
                }
            }

            return null;
        }
        public static IEnumerable<Tables.Credentials_Extended> GetCredentials()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(GetScript(), conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return ReadCredentials(reader);
                    }
                }
            }
        }
        public static void CreateOrUpdateCredentials(string name, ResourceCredentials credentials, bool allowFunctionAccess)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(GetScript(), conn))
                {
                    cmd.Parameters.AddWithValue("@CredentialType_Name", credentials.GetType().GetCustomAttribute<ScriptAliasAttribute>()?.Alias ?? credentials.GetType().Name);
                    cmd.Parameters.AddWithValue("@Credential_Name", name);
                    cmd.Parameters.AddWithValue("@EncryptedConfiguration_Xml", Encrypt(Persistence.SerializeToPersistedObjectXml(credentials)));
                    cmd.Parameters.AddWithValue("@AllowFunctionAccess_Indicator", allowFunctionAccess ? 1 : 0);

                    cmd.ExecuteNonQuery();
                }
            }
        }
        public static void DeleteCredentials(string typeName, string name)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(GetScript(), conn))
                {
                    cmd.Parameters.AddWithValue("@CredentialType_Name", typeName);
                    cmd.Parameters.AddWithValue("@Credential_Name", name);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public static void CreateOrUpdatePackageSource(string name, string url, string userName, SecureString password)
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(GetScript(), conn))
                {
                    cmd.Parameters.AddWithValue("@PackageSource_Name", name);
                    cmd.Parameters.AddWithValue("@FeedUrl_Text", url);
                    cmd.Parameters.AddWithValue("@UserName_Text", userName);
                    cmd.Parameters.AddWithValue("@EncryptedPassword_Text", Encrypt(password));

                    cmd.ExecuteNonQuery();
                }
            }
        }
        public static IEnumerable<PackageSourceData> GetPackageSources()
        {
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(GetScript(), conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return new PackageSourceData(reader);
                    }
                }
            }
        }

        private static string GetScript([CallerMemberName] string name = null)
        {
            using (var stream = typeof(RompDb).Assembly.GetManifestResourceStream(typeof(RompDb).Namespace + ".Scripts." + name + ".sql"))
            using (var reader = new StreamReader(stream, InedoLib.UTF8Encoding))
            {
                return reader.ReadToEnd();
            }
        }
        private static string Decrypt(SQLiteDataReader reader, int column)
        {
            if (reader.IsDBNull(column))
                return null;

            using (var buffer = new MemoryStream())
            {
                using (var stream = reader.GetStream(column))
                {
                    stream.CopyTo(buffer);
                }

                return InedoLib.UTF8Encoding.GetString(ProtectedData.Unprotect(buffer.ToArray(), null, RompConfig.UserMode ? DataProtectionScope.CurrentUser : DataProtectionScope.LocalMachine));
            }
        }
        private static byte[] Encrypt(string text)
        {
            if (text == null)
                return null;

            return ProtectedData.Protect(InedoLib.UTF8Encoding.GetBytes(text), null, RompConfig.UserMode ? DataProtectionScope.CurrentUser : DataProtectionScope.LocalMachine);
        }
        private static byte[] Encrypt(SecureString text)
        {
            if (text == null)
                return null;

            return ProtectedData.Protect(InedoLib.UTF8Encoding.GetBytes(AH.Unprotect(text)), null, RompConfig.UserMode ? DataProtectionScope.CurrentUser : DataProtectionScope.LocalMachine);
        }
        private static Tables.Credentials_Extended ReadCredentials(SQLiteDataReader reader)
        {
            return new Tables.Credentials_Extended
            {
                Credential_Id = reader.GetInt32(0),
                CredentialType_Name = reader.GetString(1),
                Credential_Name = reader.GetString(2),
                Configuration_Xml = Decrypt(reader, 3),
                AllowFunctionAccess_Indicator = reader.GetInt32(4) != 0
            };
        }

        public sealed class ExecutionData
        {
            public ExecutionData(SQLiteDataReader reader)
            {
                this.ExecutionId = reader.GetInt32(0);
                this.StartDate = new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero).ToLocalTime();
                if (!reader.IsDBNull(2))
                    this.EndDate = new DateTimeOffset(reader.GetInt64(2), TimeSpan.Zero).ToLocalTime();
                this.StatusCode = reader.GetString(3);
                this.RunStateCode = reader.GetString(4);
                this.ModeCode = reader.GetString(5);
                this.Simulation = reader.GetBoolean(6);
            }

            public int ExecutionId { get; }
            public DateTimeOffset StartDate { get; }
            public DateTimeOffset? EndDate { get; }
            public string StatusCode { get; }
            public string RunStateCode { get; }
            public string ModeCode { get; }
            public bool Simulation { get; }
        }

        public sealed class PackageSourceData
        {
            public PackageSourceData(SQLiteDataReader reader)
            {
                this.Name = reader.GetString(0);
                this.FeedUrl = reader.GetString(1);
                if (!reader.IsDBNull(2))
                    this.UserName = reader.GetString(2);

                var blob = !reader.IsDBNull(3) ? reader.GetFieldValue<byte[]>(3) : null;
                if (blob != null)
                    this.Password = AH.CreateSecureString(InedoLib.UTF8Encoding.GetString(ProtectedData.Unprotect(blob, null, RompConfig.UserMode ? DataProtectionScope.CurrentUser : DataProtectionScope.LocalMachine)));
            }

            public string Name { get; }
            public string FeedUrl { get; }
            public string UserName { get; }
            public SecureString Password { get; }
        }
    }
}
