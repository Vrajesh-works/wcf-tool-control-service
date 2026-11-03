namespace ToolControl.Core
{
    public enum RunState
    {
        Idle,
        Running,
        Paused,
        Completed,
        Aborted
    }

    public enum AlarmSeverity
    {
        Warning,
        Critical
    }
}
