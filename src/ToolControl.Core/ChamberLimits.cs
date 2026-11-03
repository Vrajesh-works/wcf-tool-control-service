namespace ToolControl.Core
{
    /// <summary>Safe operating envelope for a chamber. Values are validated by the controller, never trusted from a caller.</summary>
    public sealed class ChamberLimits
    {
        public ChamberLimits(double minPressureMTorr, double maxPressureMTorr, double minTemperatureC, double maxTemperatureC)
        {
            MinPressureMTorr = minPressureMTorr;
            MaxPressureMTorr = maxPressureMTorr;
            MinTemperatureC = minTemperatureC;
            MaxTemperatureC = maxTemperatureC;
        }

        public static ChamberLimits Default { get; } = new ChamberLimits(0, 500, 20, 400);

        public double MinPressureMTorr { get; }
        public double MaxPressureMTorr { get; }
        public double MinTemperatureC { get; }
        public double MaxTemperatureC { get; }
        public bool IsPressureOutOfRange(double pressure) => pressure < MinPressureMTorr || pressure > MaxPressureMTorr;
        public bool IsTemperatureOutOfRange(double temperature) => temperature < MinTemperatureC || temperature > MaxTemperatureC;
    }
}
