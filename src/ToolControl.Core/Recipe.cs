using System;

namespace ToolControl.Core
{
    public sealed class Recipe
    {
        public Recipe(string name, double targetPressureMTorr, double targetTemperatureC, TimeSpan duration)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Recipe name is required.", nameof(name));
            if (duration <= TimeSpan.Zero) throw new ArgumentException("Recipe duration must be positive.", nameof(duration));
            Name = name;
            TargetPressureMTorr = targetPressureMTorr;
            TargetTemperatureC = targetTemperatureC;
            Duration = duration;
        }

        public string Name { get; }
        public double TargetPressureMTorr { get; }
        public double TargetTemperatureC { get; }
        public TimeSpan Duration { get; }
    }
}
