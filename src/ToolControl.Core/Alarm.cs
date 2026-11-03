using System;

namespace ToolControl.Core
{
    public sealed class Alarm
    {
        public Alarm(string id, AlarmSeverity severity, string message, DateTime raisedAtUtc)
        {
            Id = id;
            Severity = severity;
            Message = message;
            RaisedAtUtc = raisedAtUtc;
        }

        public string Id { get; }
        public AlarmSeverity Severity { get; }
        public string Message { get; }
        public DateTime RaisedAtUtc { get; }
        public DateTime? AcknowledgedAtUtc { get; private set; }
        public bool Acknowledged => AcknowledgedAtUtc.HasValue;
        public void Acknowledge(DateTime atUtc) => AcknowledgedAtUtc = AcknowledgedAtUtc ?? atUtc;
    }
}
