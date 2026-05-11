namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents persisted daemon launch-attempt <c>startup-diagnosis.json</c> contract fields. </summary>
internal sealed record DaemonLaunchAttemptJsonContract (
    int? SchemaVersion,
    string? LaunchAttemptId,
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
    DaemonDiagnosisJsonContract? Diagnosis);
