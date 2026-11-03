using System;

namespace ToolControl.Core
{
    /// <summary>One live sample from a process chamber. All timestamps are UTC.</summary>
    public sealed class ChamberReading
    {
        public ChamberReading(DateTime timestampUtc, double pressureMTorr, double temperatureC)
        {
            TimestampUtc = timestampUtc.Kind == DateTimeKind.Utc
                ? timestampUtc
                : timestampUtc.ToUniversalTime();
            PressureMTorr = pressureMTorr;
            TemperatureC = temperatureC;
        }

        public DateTime TimestampUtc { get; }
        public double PressureMTorr { get; }
        public double TemperatureC { get; }
    }
}
