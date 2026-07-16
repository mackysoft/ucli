using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents persisted daemon launch-attempt <c>startup-diagnosis.json</c> contract fields. </summary>
internal sealed record DaemonLaunchAttemptJsonContract
{
    /// <summary> Initializes one persisted daemon launch-attempt contract. </summary>
    [JsonConstructor]
    public DaemonLaunchAttemptJsonContract (
        int? SchemaVersion,
        Guid? LaunchAttemptId,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        string? StartupStatus,
        string? StartupBlockingReason,
        string? RetryDisposition,
        string? ProcessAction,
        string? EditorMode,
        int? ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        string? UnityLogPath,
        DaemonDiagnosisJsonContract? Diagnosis)
    {
        if (LaunchAttemptId == Guid.Empty)
        {
            throw new ArgumentException("Launch attempt identifier must not be empty.", nameof(LaunchAttemptId));
        }

        this.SchemaVersion = SchemaVersion;
        this.LaunchAttemptId = LaunchAttemptId;
        this.StartedAtUtc = StartedAtUtc;
        this.UpdatedAtUtc = UpdatedAtUtc;
        this.StartupStatus = StartupStatus;
        this.StartupBlockingReason = StartupBlockingReason;
        this.RetryDisposition = RetryDisposition;
        this.ProcessAction = ProcessAction;
        this.EditorMode = EditorMode;
        this.ProcessId = ProcessId;
        this.ProcessStartedAtUtc = ProcessStartedAtUtc;
        this.UnityLogPath = UnityLogPath;
        this.Diagnosis = Diagnosis;
    }

    public int? SchemaVersion { get; }

    public Guid? LaunchAttemptId { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public string? StartupStatus { get; }

    public string? StartupBlockingReason { get; }

    public string? RetryDisposition { get; }

    public string? ProcessAction { get; }

    public string? EditorMode { get; }

    public int? ProcessId { get; }

    public DateTimeOffset? ProcessStartedAtUtc { get; }

    public string? UnityLogPath { get; }

    public DaemonDiagnosisJsonContract? Diagnosis { get; }
}
