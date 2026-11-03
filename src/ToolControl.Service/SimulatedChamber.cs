using System;
using ToolControl.Core;

namespace ToolControl.Service
{
    /// <summary>
    /// Hardware stand-in for development and demos. It random-walks around the
    /// active recipe targets so the service can run with no physical tool attached.
    /// Replace with a real hardware driver behind IChamberHardware in production.
    /// </summary>
    public interface IChamberHardware
    {
        ChamberReading Read(Recipe activeRecipe);
    }

    public sealed class SimulatedChamber : IChamberHardware
    {
        private readonly Random _random = new Random();
        private double _pressure = 120;
        private double _temperature = 150;

        public ChamberReading Read(Recipe activeRecipe)
        {
            var targetP = activeRecipe?.TargetPressureMTorr ?? 120;
            var targetT = activeRecipe?.TargetTemperatureC ?? 150;
            _pressure += (targetP - _pressure) * 0.05 + (_random.NextDouble() - 0.5) * 4;
            _temperature += (targetT - _temperature) * 0.05 + (_random.NextDouble() - 0.5) * 2;
            return new ChamberReading(DateTime.UtcNow, Math.Round(_pressure, 2), Math.Round(_temperature, 2));
        }
    }
}
