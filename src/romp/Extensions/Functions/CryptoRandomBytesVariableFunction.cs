using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Romp.Extensions.Functions
{
    [ScriptAlias("CryptoRandomBytes")]
    [DisplayName("Generate Cryptographic Random Bytes")]
    [Description("Returns a containing hex characters that are cryptographically random.")]
    public sealed class CryptoRandomBytesVariableFunction : ScalarVariableFunction
    {
        [DisplayName("count")]
        [VariableFunctionParameter(0)]
        [Description("The number of bytes to generate. The returned string will be twice this length in characters.")]
        public int Count { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            if (this.Count < 0 || this.Count > 1024 * 1024)
                throw new ExecutionFailureException("Invalid count specified for $CryptoRandomBytes function.");
            if (this.Count == 0)
                return string.Empty;

            var bytes = new byte[this.Count];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            var buffer = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                buffer.Append(b.ToString("x2"));

            return buffer.ToString();
        }
    }
}
