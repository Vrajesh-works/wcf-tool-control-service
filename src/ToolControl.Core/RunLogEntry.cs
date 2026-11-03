using System;

namespace ToolControl.Core
{
    public sealed class RunLogEntry
    {
        public RunLogEntry(DateTime timestampUtc, string message)
        {
            TimestampUtc = timestampUtc;
            Message = message;
        }

        public DateTime TimestampUtc { get; }
        public string Message { get; }
        public override string ToString() => $"[{TimestampUtc:O}] {Message}";
    }
}
