using System;
using System.Collections.Generic;

namespace ToolControl.Core
{
    /// <summary>
    /// Pure, testable chamber run state machine. It owns no I/O, no WCF and no UI,
    /// so it can be unit tested in isolation and reused by any host.
    /// All public members are thread-safe.
    /// </summary>
    public sealed class ChamberController
    {
        private readonly object _gate = new object();
        private readonly List<RunLogEntry> _log = new List<RunLogEntry>();
        private readonly List<Alarm> _alarms = new List<Alarm>();
        private readonly ChamberLimits _limits;
        private const int MaxLogEntries = 10000;

        public ChamberController(ChamberLimits limits = null)
        {
            _limits = limits ?? ChamberLimits.Default;
        }

        public RunState State { get; private set; } = RunState.Idle;
        public Recipe ActiveRecipe { get; private set; }
        public ChamberReading LastReading { get; private set; }
        public TelemetryBroadcaster Broadcaster { get; } = new TelemetryBroadcaster();

        public IReadOnlyList<RunLogEntry> GetLog()
        {
            lock (_gate) { return _log.ToArray(); }
        }

        public IReadOnlyList<Alarm> GetActiveAlarms()
        {
            lock (_gate)
            {
                var active = new List<Alarm>();
                foreach (var alarm in _alarms) if (!alarm.Acknowledged) active.Add(alarm);
                return active;
            }
        }

        public void StartRecipe(Recipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            lock (_gate)
            {
                if (State == RunState.Running || State == RunState.Paused)
                    throw new InvalidOperationException("A run is already active. Abort it before starting a new recipe.");
                if (_limits.IsPressureOutOfRange(recipe.TargetPressureMTorr) || _limits.IsTemperatureOutOfRange(recipe.TargetTemperatureC))
                    throw new ArgumentException("Recipe targets are outside the safe chamber limits.", nameof(recipe));
                ActiveRecipe = recipe;
                State = RunState.Running;
                AppendLogLocked($"Recipe started: {recipe.Name} (pressure {recipe.TargetPressureMTorr} mTorr, temperature {recipe.TargetTemperatureC} C).");
            }
        }

        public void Pause()
        {
            lock (_gate)
            {
                if (State != RunState.Running) throw new InvalidOperationException("Only a running run can be paused.");
                State = RunState.Paused;
                AppendLogLocked("Run paused by operator.");
            }
        }

        public void Resume()
        {
            lock (_gate)
            {
                if (State != RunState.Paused) throw new InvalidOperationException("Only a paused run can be resumed.");
                State = RunState.Running;
                AppendLogLocked("Run resumed by operator.");
            }
        }

        public void Abort()
        {
            lock (_gate)
            {
                if (State != RunState.Running && State != RunState.Paused) throw new InvalidOperationException("No active run to abort.");
                State = RunState.Aborted;
                AppendLogLocked("Run aborted by operator.");
            }
        }

        public bool AcknowledgeAlarm(string alarmId)
        {
            lock (_gate)
            {
                foreach (var alarm in _alarms)
                {
                    if (alarm.Id == alarmId && !alarm.Acknowledged)
                    {
                        alarm.Acknowledge(DateTime.UtcNow);
                        AppendLogLocked($"Alarm acknowledged: {alarm.Id} ({alarm.Message}).");
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Called by the control loop for every new sample. Never blocks on clients:
        /// fan-out goes through the broadcaster's bounded per-client queues.
        /// </summary>
        public void OnReading(ChamberReading reading)
        {
            if (reading == null) throw new ArgumentNullException(nameof(reading));
            lock (_gate)
            {
                LastReading = reading;
                if (State == RunState.Running)
                {
                    if (_limits.IsPressureOutOfRange(reading.PressureMTorr))
                        RaiseAlarmLocked(AlarmSeverity.Critical, $"Pressure out of range: {reading.PressureMTorr} mTorr.");
                    if (_limits.IsTemperatureOutOfRange(reading.TemperatureC))
                        RaiseAlarmLocked(AlarmSeverity.Critical, $"Temperature out of range: {reading.TemperatureC} C.");
                }
            }
            Broadcaster.Publish(reading);
        }

        private void RaiseAlarmLocked(AlarmSeverity severity, string message)
        {
            var alarm = new Alarm(Guid.NewGuid().ToString("N"), severity, message, DateTime.UtcNow);
            _alarms.Add(alarm);
            AppendLogLocked($"ALARM [{severity}] {message}");
        }

        private void AppendLogLocked(string message)
        {
            _log.Add(new RunLogEntry(DateTime.UtcNow, message));
            while (_log.Count > MaxLogEntries) _log.RemoveAt(0);
        }
    }
}
