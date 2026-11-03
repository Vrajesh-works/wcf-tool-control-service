using System;
using System.ServiceModel;
using System.Windows.Forms;
using ToolControl.Contracts;

namespace ToolControl.OperatorConsole
{
    /// <summary>
    /// Operator console. Live readings arrive on WCF callbacks; alarms and the
    /// run log are polled on a UI timer so a missed callback can never hide an alarm.
    /// All service calls are wrapped so a faulted channel shows an error instead of crashing the UI.
    /// </summary>
    public sealed class MainForm : Form, IToolControlCallback
    {
        private readonly ComboBox _recipes = new ComboBox { Left = 12, Top = 12, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Button _start = new Button { Text = "Start recipe", Left = 240, Top = 10, Width = 110 };
        private readonly Button _pause = new Button { Text = "Pause", Left = 358, Top = 10, Width = 80 };
        private readonly Button _resume = new Button { Text = "Resume", Left = 446, Top = 10, Width = 80 };
        private readonly Button _abort = new Button { Text = "Abort", Left = 534, Top = 10, Width = 80 };
        private readonly Label _status = new Label { Left = 12, Top = 44, Width = 600, Text = "Status: not connected" };
        private readonly ListBox _readings = new ListBox { Left = 12, Top = 70, Width = 600, Height = 180 };
        private readonly ListBox _alarms = new ListBox { Left = 12, Top = 260, Width = 500, Height = 110 };
        private readonly Button _ack = new Button { Text = "Acknowledge selected alarm", Left = 520, Top = 260, Width = 170 };
        private readonly TextBox _log = new TextBox { Left = 12, Top = 380, Width = 600, Height = 150, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true };
        private readonly Timer _refresh = new Timer { Interval = 2000 };

        private IToolControlService _client;
        private DuplexChannelFactory<IToolControlService> _factory;

        public MainForm()
        {
            Text = "Tool Control - Operator Console";
            Width = 650;
            Height = 580;
            _recipes.Items.AddRange(new object[] { "Standard Etch|120|150|30", "High Temp Bake|200|300|45", "Low Pressure Clean|80|100|15" });
            _recipes.SelectedIndex = 0;
            _start.Click += (_, __) => Safe(() =>
            {
                var parts = ((string)_recipes.SelectedItem).Split('|');
                _client.StartRecipe(parts[0], double.Parse(parts[1]), double.Parse(parts[2]), double.Parse(parts[3]));
            });
            _pause.Click += (_, __) => Safe(() => _client.PauseRun());
            _resume.Click += (_, __) => Safe(() => _client.ResumeRun());
            _abort.Click += (_, __) => Safe(() => _client.AbortRun());
            _ack.Click += (_, __) => Safe(() =>
            {
                if (_alarms.SelectedItem is string text)
                {
                    var id = text.Split(' ')[0];
                    _client.AcknowledgeAlarm(id);
                }
            });
            _refresh.Tick += (_, __) => Safe(RefreshStatus);
            Controls.AddRange(new Control[] { _recipes, _start, _pause, _resume, _abort, _status, _readings, _alarms, _log, _ack });
            Load += (_, __) => Connect();
            FormClosing += (_, __) => { try { _client?.Unsubscribe(); } catch { /* shutting down */ } _factory?.Close(); };
        }

        private void Connect()
        {
            try
            {
                var binding = new NetTcpBinding(SecurityMode.None) { ReliableSession = { Enabled = true } };
                _factory = new DuplexChannelFactory<IToolControlService>(this, binding, new EndpointAddress("net.tcp://localhost:9000/ToolControl"));
                _client = _factory.CreateChannel();
                _client.Subscribe();
                _status.Text = "Status: connected";
                _refresh.Start();
            }
            catch (Exception ex) { _status.Text = "Status: connection failed - " + ex.Message; }
        }

        private void Safe(Action action)
        {
            try { action(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Tool Control", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void RefreshStatus()
        {
            var status = _client.GetStatus();
            _status.Text = $"Status: {status.State} | Recipe: {status.RecipeName ?? "-"} | Clients: {status.SubscriberCount}";
            _alarms.Items.Clear();
            foreach (var alarm in status.ActiveAlarms) _alarms.Items.Add($"{alarm.Id} [{alarm.Severity}] {alarm.Message}");
            _log.Lines = _client.GetRunLog().ToArray();
        }

        public void OnReading(ReadingDto reading)
        {
            if (InvokeRequired) { BeginInvoke(new Action<ReadingDto>(OnReading), reading); return; }
            _readings.Items.Insert(0, $"{reading.TimestampUtc:HH:mm:ss}  {reading.PressureMTorr} mTorr  {reading.TemperatureC} C");
            while (_readings.Items.Count > 50) _readings.Items.RemoveAt(_readings.Items.Count - 1);
        }

        public void OnAlarm(AlarmDto alarm) { /* Alarms are also picked up by the 2s refresh, so a missed callback cannot hide one. */ }
        public void OnStatusChanged(string state) { if (InvokeRequired) BeginInvoke(new Action(() => _status.Text = "Status: " + state)); }
    }
}
