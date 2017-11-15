using Inedo.Data;

namespace Inedo.Romp.Data
{
    internal static class Domains
    {
        public abstract class ExecutionRunState : DataDomain<ExecutionRunState>
        {
            public static readonly string Pending = "P";
            public static readonly string Executing = "X";
            public static readonly string Completed = "C";

            private ExecutionRunState() { }
        }

        public abstract class ExecutionStatus : DataDomain<ExecutionStatus>
        {
            public static readonly string Normal = "S";
            public static readonly string Warning = "W";
            public static readonly string Error = "E";

            private ExecutionStatus() { }
        }
    }
}
