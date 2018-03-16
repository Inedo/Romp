using System;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Romp.Extensions.Operations
{
    [Serializable]
    [ScriptAlias("Execute-Query")]
    [ScriptNamespace("SqlServer", PreferUnqualified = false)]
    [DisplayName("Execute SQL Query")]
    [Description("Executes a SQL query against an instance of SQL Server.")]
    [Example(@"# Stores the ID of the account named $accountName into the $accountId variable
SqlServer::Execute-Query
(
    ConnectionString: Data Source=localhost; Initial Catalog=MyDatabase; Integrated Security=True;,
    Query: >>SELECT [Account_Id] FROM [Accounts] WHERE [Account_Name] = '$EscapeSqlString($accountName)'>>,
    ScalarResult => $accountId
);
 ")]
    public sealed class ExecuteSqlQueryOperation : RemoteExecuteOperation
    {
        [Required]
        [ScriptAlias("ConnectionString")]
        public string ConnectionString { get; set; }
        [Required]
        [ScriptAlias("Query")]
        public string Query { get; set; }
        [Output]
        [ScriptAlias("ScalarResult")]
        [Description("The value of the first column of the first row in the returned result set will be written to this variable.")]
        public string ScalarOutput { get; set; }

        protected override async Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            using (var sqlConnection = new SqlConnection(this.ConnectionString))
            {
                this.LogDebug("Connecting to SQL Server...");
                await sqlConnection.OpenAsync(context.CancellationToken);

                this.LogDebug("Connection established; executing query...");
                using (var sqlCommand = new SqlCommand(this.Query, sqlConnection))
                {
                    sqlCommand.CommandTimeout = 0; // use cancellation token for timeouts

                    this.ScalarOutput = (await sqlCommand.ExecuteScalarAsync(context.CancellationToken))?.ToString() ?? string.Empty;
                    this.LogInformation($"Query completed (returned {this.ScalarOutput})");
                }
            }

            return null;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var shortDesc = new RichDescription("Execute SQL Server Query ", new Hilite(config[nameof(Query)]));

            if (config.OutArguments.TryGetValue("ScalarResult", out var variableName))
            {
                return new ExtendedRichDescription(
                    shortDesc,
                    new RichDescription("and store scalar output in ", new Hilite(variableName.ToString()))
                );
            }

            return new ExtendedRichDescription(shortDesc);
        }
    }
}
