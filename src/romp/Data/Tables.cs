using Inedo.Data;

namespace Inedo.Romp.Data
{
    internal static class Tables
    {
        public sealed class Credentials_Extended
        {
            public int Credential_Id { get; set; }
            public int? Environment_Id { get; set; }
            public string Credential_Name { get; set; }
            public string CredentialType_Name { get; set; }
            public string Configuration_Xml { get; set; }
            public YNIndicator AllowFunctionAccess_Indicator { get; set; }
            public string Environment_Name { get; set; }
        }

        public class Rafts
        {
            public int Raft_Id { get; set; }
            public string Raft_Name { get; set; }
            public int? Project_Id { get; set; }
            public int? Environment_Id { get; set; }
            public string Raft_Configuration { get; set; }
        }
    }
}
