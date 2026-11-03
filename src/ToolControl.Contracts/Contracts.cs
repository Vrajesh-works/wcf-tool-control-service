using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace ToolControl.Contracts
{
    [DataContract]
    public sealed class ReadingDto
    {
        [DataMember] public DateTime TimestampUtc { get; set; }
        [DataMember] public double PressureMTorr { get; set; }
        [DataMember] public double TemperatureC { get; set; }
    }

    [DataContract]
    public sealed class AlarmDto
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Severity { get; set; }
        [DataMember] public string Message { get; set; }
        [DataMember] public DateTime RaisedAtUtc { get; set; }
        [DataMember] public bool Acknowledged { get; set; }
    }

    [DataContract]
    public sealed class RunStatusDto
    {
        [DataMember] public string State { get; set; }
        [DataMember] public string RecipeName { get; set; }
        [DataMember] public ReadingDto LastReading { get; set; }
        [DataMember] public List<AlarmDto> ActiveAlarms { get; set; }
        [DataMember] public int SubscriberCount { get; set; }
    }

    /// <summary>Server-to-client callbacks. One-way: a slow client must never block the service.</summary>
    public interface IToolControlCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnReading(ReadingDto reading);

        [OperationContract(IsOneWay = true)]
        void OnAlarm(AlarmDto alarm);

        [OperationContract(IsOneWay = true)]
        void OnStatusChanged(string state);
    }

    [ServiceContract(CallbackContract = typeof(IToolControlCallback), SessionMode = SessionMode.Required)]
    public interface IToolControlService
    {
        [OperationContract(IsInitiating = true)]
        void Subscribe();

        [OperationContract(IsTerminating = true)]
        void Unsubscribe();

        [OperationContract]
        void StartRecipe(string recipeName, double targetPressureMTorr, double targetTemperatureC, double durationMinutes);

        [OperationContract]
        void PauseRun();

        [OperationContract]
        void ResumeRun();

        [OperationContract]
        void AbortRun();

        [OperationContract]
        bool AcknowledgeAlarm(string alarmId);

        [OperationContract]
        RunStatusDto GetStatus();

        [OperationContract]
        List<string> GetRunLog();
    }
}
