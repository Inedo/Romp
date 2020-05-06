using System;
using Inedo.Diagnostics;

namespace Inedo.Romp.RompExecutionEngine
{
    internal interface IExecutionLogMessage
    {
        MessageLevel Level { get; }
        string Message { get; }
        DateTime DateTime { get; }
        int Sequence { get; }
    }
}
