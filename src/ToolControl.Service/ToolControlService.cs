using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ToolControl.Contracts;
using ToolControl.Core;

namespace ToolControl.Service
{
    /// <summary>
    /// WCF duplex service. One shared controller owns the run state; every client
    /// gets its own bounded queue and dispatcher task, so a slow or dead client
    /// can never block the control loop or other clients.
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public sealed class ToolControlService : IToolControlService, IDisposable
    {
        private static readonly ChamberController Controller = new ChamberController();
        private static readonly IChamberHardware Hardware = new SimulatedChamber();
        private static readonly System.Timers.Timer ControlLoop;
        private static int _controlLoopStarted;

        static ToolControlService()
        {
            ControlLoop = new System.Timers.Timer(500) { AutoReset = true };
            ControlLoop.Elapsed += (_, __) =>
            {
                try { Controller.OnReading(Hardware.Read(Controller.ActiveRecipe)); }
                catch { /* Control loop must survive a bad sample; faults are logged by the host. */ }
            };
        }

        private string _clientId;
        private SubscriberQueue<ChamberReading> _queue;
        private CancellationTokenSource _dispatcherCts;
        private Task _dispatcher;

        public void Subscribe()
        {
            _clientId = OperationContext.Current?.SessionId ?? Guid.NewGuid().ToString("N");
            _queue = Controller.Broadcaster.Subscribe(_clientId);
            var callback = OperationContext.Current.GetCallbackChannel<IToolControlCallback>();
            _dispatcherCts = new CancellationTokenSource();
            var token = _dispatcherCts.Token;
            _dispatcher = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    ChamberReading reading;
                    try { reading = await _queue.TakeAsync(token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    try
                    {
                        callback.OnReading(new ReadingDto
                        {
                            TimestampUtc = reading.TimestampUtc,
                            PressureMTorr = reading.PressureMTorr,
                            TemperatureC = reading.TemperatureC
                        });
                    }
                    catch (CommunicationException) { break; }
                    catch (TimeoutException) { break; }
                }
            }, token);

            if (Interlocked.Exchange(ref _controlLoopStarted, 1) == 0) ControlLoop.Start();
        }

        public void Unsubscribe()
        {
            _dispatcherCts?.Cancel();
            if (!string.IsNullOrEmpty(_clientId)) Controller.Broadcaster.Unsubscribe(_clientId);
        }

        public void StartRecipe(string recipeName, double targetPressureMTorr, double targetTemperatureC, double durationMinutes)
        {
            Controller.StartRecipe(new Recipe(recipeName, targetPressureMTorr, targetTemperatureC, TimeSpan.FromMinutes(durationMinutes)));
            NotifyStatus();
        }

        public void PauseRun() { Controller.Pause(); NotifyStatus(); }
        public void ResumeRun() { Controller.Resume(); NotifyStatus(); }
        public void AbortRun() { Controller.Abort(); NotifyStatus(); }

        public bool AcknowledgeAlarm(string alarmId) => Controller.AcknowledgeAlarm(alarmId);

        public RunStatusDto GetStatus()
        {
            var status = new RunStatusDto
            {
                State = Controller.State.ToString(),
                RecipeName = Controller.ActiveRecipe?.Name,
                SubscriberCount = Controller.Broadcaster.SubscriberCount,
                ActiveAlarms = Controller.GetActiveAlarms().Select(a => new AlarmDto
                {
                    Id = a.Id, Severity = a.Severity.ToString(), Message = a.Message,
                    RaisedAtUtc = a.RaisedAtUtc, Acknowledged = a.Acknowledged
                }).ToList()
            };
            if (Controller.LastReading != null)
            {
                status.LastReading = new ReadingDto
                {
                    TimestampUtc = Controller.LastReading.TimestampUtc,
                    PressureMTorr = Controller.LastReading.PressureMTorr,
                    TemperatureC = Controller.LastReading.TemperatureC
                };
            }
            return status;
        }

        public List<string> GetRunLog() => Controller.GetLog().Select(e => e.ToString()).ToList();

        private void NotifyStatus()
        {
            // Status changes are cheap and infrequent; best-effort only.
            try
            {
                OperationContext.Current?.GetCallbackChannel<IToolControlCallback>()
                    ?.OnStatusChanged(Controller.State.ToString());
            }
            catch { /* A dead callback must not fault the operator command. */ }
        }

        public void Dispose()
        {
            _dispatcherCts?.Cancel();
            _dispatcherCts?.Dispose();
        }
    }
}
