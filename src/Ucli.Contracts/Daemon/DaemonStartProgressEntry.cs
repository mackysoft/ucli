namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one host-visible <c>daemon.start</c> progress stream payload. </summary>
public sealed record DaemonStartProgressEntry (
    ProjectFingerprint ProjectFingerprint,
    int TimeoutMilliseconds,
    string? EditorMode,
    string OnStartupBlocked,
    string? Result,
    string? StartStatus,
    string? DaemonStatus,
    string? ErrorCode);
