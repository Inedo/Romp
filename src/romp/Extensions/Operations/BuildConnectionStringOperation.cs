using System.ComponentModel;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Romp.Extensions.Operations
{
    [ScriptAlias("Build-ConnectionString")]
    [ScriptNamespace("SqlServer", PreferUnqualified = false)]
    [DisplayName("Build SQL Server Connection String")]
    [Description("Creates or modifies a SQL Server connection string using its component properties.")]
    public sealed class BuildConnectionStringOperation : ExecuteOperation
    {
        [ScriptAlias("From")]
        [DisplayName("Start from")]
        public string From { get; set; }

        [ScriptAlias("DataSource")]
        [DisplayName("Data Source")]
        public string DataSource { get; set; }
        [ScriptAlias("InitialCatalog")]
        [DisplayName("Initial Catalog")]
        public string InitialCatalog { get; set; }
        [ScriptAlias("UserID")]
        [DisplayName("User ID")]
        public string UserID { get; set; }
        [ScriptAlias("Password")]
        public string Password { get; set; }
        [ScriptAlias("IntegratedSecurity")]
        [DisplayName("Integrated Security")]
        public bool? IntegratedSecurity { get; set; }

        [Category("Advanced")]
        [ScriptAlias("MaxPoolSize")]
        [DisplayName("Max Pool Size")]
        public int? MaxPoolSize { get; set; }
        [Category("Advanced")]
        [ScriptAlias("ConnectRetryCount")]
        [DisplayName("Connect Retry Count")]
        public int? ConnectRetryCount { get; set; }
        [Category("Advanced")]
        [ScriptAlias("ConnectRetryInterval")]
        [DisplayName("Connect Retry Interval")]
        public int? ConnectRetryInterval { get; set; }
        [Category("Advanced")]
        [ScriptAlias("MinPoolSize")]
        [DisplayName("Min Pool Size")]
        public int? MinPoolSize { get; set; }
        [Category("Advanced")]
        [ScriptAlias("MultipleActiveResultSets")]
        [DisplayName("Multiple Active Result Sets")]
        public bool? MultipleActiveResultSets { get; set; }
        [Category("Advanced")]
        [ScriptAlias("MultiSubnetFailover")]
        [DisplayName("Multi-Subnet Failover")]
        public bool? MultiSubnetFailover { get; set; }
        [Category("Advanced")]
        [ScriptAlias("NetworkLibrary")]
        [DisplayName("Network Library")]
        public string NetworkLibrary { get; set; }
        [Category("Advanced")]
        [ScriptAlias("PacketSize")]
        [DisplayName("Packet Size")]
        public int? PacketSize { get; set; }
        [Category("Advanced")]
        [ScriptAlias("PersistSecurityInfo")]
        [DisplayName("Persist Security Info")]
        public bool? PersistSecurityInfo { get; set; }
        [Category("Advanced")]
        [ScriptAlias("Pooling")]
        public bool? Pooling { get; set; }
        [Category("Advanced")]
        [ScriptAlias("Replication")]
        public bool? Replication { get; set; }
        [Category("Advanced")]
        [ScriptAlias("TransactionBinding")]
        [DisplayName("Transaction Binding")]
        public string TransactionBinding { get; set; }
        [Category("Advanced")]
        [ScriptAlias("TypeSystemVersion")]
        [DisplayName("Type System Version")]
        public string TypeSystemVersion { get; set; }
        [Category("Advanced")]
        [ScriptAlias("UserInstance")]
        [DisplayName("User Instance")]
        public bool? UserInstance { get; set; }
        [Category("Advanced")]
        [ScriptAlias("WorkstationID")]
        [DisplayName("Workstation ID")]
        public string WorkstationID { get; set; }
        [Category("Advanced")]
        [ScriptAlias("LoadBalanceTimeout")]
        [DisplayName("Load Balance Timeout")]
        public int? LoadBalanceTimeout { get; set; }
        [Category("Advanced")]
        [ScriptAlias("FailoverPartner")]
        [DisplayName("Failover Partner")]
        public string FailoverPartner { get; set; }
        [Category("Advanced")]
        [ScriptAlias("ApplicationIntent")]
        [DisplayName("Application Intent")]
        public ApplicationIntent? ApplicationIntent { get; set; }
        [Category("Advanced")]
        [ScriptAlias("ApplicationName")]
        [DisplayName("Application Name")]
        public string ApplicationName { get; set; }
        [Category("Advanced")]
        [ScriptAlias("AttachDBFilename")]
        [DisplayName("Attach DB File name")]
        public string AttachDBFilename { get; set; }
        [Category("Advanced")]
        [ScriptAlias("ContextConnection")]
        [DisplayName("Context Connection")]
        public bool? ContextConnection { get; set; }
        [Category("Advanced")]
        [ScriptAlias("AsynchronousProcessing")]
        [DisplayName("Asynchronous Processing")]
        public bool? AsynchronousProcessing { get; set; }
        [Category("Advanced")]
        [ScriptAlias("CurrentLanguage")]
        [DisplayName("Current Language")]
        public string CurrentLanguage { get; set; }
        [Category("Advanced")]
        [ScriptAlias("Encrypt")]
        public bool? Encrypt { get; set; }
        [Category("Advanced")]
        [ScriptAlias("TrustServerCertificate")]
        [DisplayName("Trust Server Certificate")]
        public bool? TrustServerCertificate { get; set; }
        [Category("Advanced")]
        [ScriptAlias("Enlist")]
        public bool? Enlist { get; set; }
        [Category("Advanced")]
        [ScriptAlias("ConnectTimeout")]
        [DisplayName("Connect Timeout")]
        public int? ConnectTimeout { get; set; }

        [Output]
        [ScriptAlias("To")]
        public string To { get; set; }

        public override Task ExecuteAsync(IOperationExecutionContext context)
        {
            var b = string.IsNullOrWhiteSpace(this.From) ? new SqlConnectionStringBuilder() : new SqlConnectionStringBuilder(this.From);

            if (this.MaxPoolSize.HasValue)
                b.MaxPoolSize = this.MaxPoolSize.Value;
            if (this.ConnectRetryCount.HasValue)
                b.ConnectRetryCount = this.ConnectRetryCount.Value;
            if (this.MinPoolSize.HasValue)
                b.MinPoolSize = this.MaxPoolSize.Value;
            if (this.MultipleActiveResultSets.HasValue)
                b.MultipleActiveResultSets = this.MultipleActiveResultSets.Value;
            if (this.MultiSubnetFailover.HasValue)
                b.MultiSubnetFailover = this.MultiSubnetFailover.Value;
            if (this.NetworkLibrary != null)
                b.NetworkLibrary = this.NetworkLibrary;
            if (this.PacketSize.HasValue)
                b.PacketSize = this.PacketSize.Value;
            if (this.Password != null)
                b.Password = this.Password;
            if (this.PersistSecurityInfo.HasValue)
                b.PersistSecurityInfo = this.PersistSecurityInfo.Value;
            if (this.Pooling.HasValue)
                b.Pooling = this.Pooling.Value;
            if (this.Replication.HasValue)
                b.Replication = this.Replication.Value;
            if (this.TransactionBinding != null)
                b.TransactionBinding = this.TransactionBinding;
            if (this.TypeSystemVersion != null)
                b.TypeSystemVersion = this.TypeSystemVersion;
            if (this.UserID != null)
                b.UserID = this.UserID;
            if (this.UserInstance.HasValue)
                b.UserInstance = this.UserInstance.Value;
            if (this.WorkstationID != null)
                b.WorkstationID = this.WorkstationID;
            if (this.LoadBalanceTimeout.HasValue)
                b.LoadBalanceTimeout = this.LoadBalanceTimeout.Value;
            if (this.IntegratedSecurity.HasValue)
                b.IntegratedSecurity = this.IntegratedSecurity.Value;
            if (this.InitialCatalog != null)
                b.InitialCatalog = this.InitialCatalog;
            if (this.FailoverPartner != null)
                b.FailoverPartner = this.FailoverPartner;
            if (this.ApplicationIntent.HasValue)
                b.ApplicationIntent = this.ApplicationIntent.Value;
            if (this.ApplicationName != null)
                b.ApplicationName = this.ApplicationName;
            if (this.AttachDBFilename != null)
                b.AttachDBFilename = this.AttachDBFilename;
            if (this.ContextConnection.HasValue)
                b.ContextConnection = this.ContextConnection.Value;
            if (this.AsynchronousProcessing.HasValue)
                b.AsynchronousProcessing = this.AsynchronousProcessing.Value;
            if (this.CurrentLanguage != null)
                b.CurrentLanguage = this.CurrentLanguage;
            if (this.DataSource != null)
                b.DataSource = this.DataSource;
            if (this.Encrypt.HasValue)
                b.Encrypt = this.Encrypt.Value;
            if (this.TrustServerCertificate.HasValue)
                b.TrustServerCertificate = this.TrustServerCertificate.Value;
            if (this.Enlist.HasValue)
                b.Enlist = this.Enlist.Value;
            if (this.ConnectTimeout.HasValue)
                b.ConnectTimeout = this.ConnectTimeout.Value;

            this.To = b.ToString();

            return Complete;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Write SQL Connection String to ",
                    new Hilite(config.OutArguments.GetValueOrDefault("To")?.ToString() ?? string.Empty)
                )
            );
        }
    }
}
