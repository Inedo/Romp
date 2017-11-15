using System;
using System.Runtime.Serialization;

namespace Inedo.Romp
{
    [Serializable]
    internal sealed class RompException : Exception
    {
        public RompException()
        {
        }
        public RompException(string message)
            : base(message)
        {
        }
        public RompException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
        private RompException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
