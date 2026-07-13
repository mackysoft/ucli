namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one host-visible <c>daemon.start</c> progress stream payload. </summary>
public sealed record DaemonStartProgressEntry (
    string ProjectFingerprint,
    int TimeoutMilliseconds,
    DaemonEditorMode? EditorMode,
    DaemonStartupBlockedProcessPolicy OnStartupBlocked,
    CommandProgressResult? Result,
    string? StartStatus,
    string? DaemonStatus,
    string? ErrorCode);
