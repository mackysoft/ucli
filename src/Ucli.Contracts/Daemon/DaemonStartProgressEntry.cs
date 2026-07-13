using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one host-visible <c>daemon.start</c> progress stream payload. </summary>
public sealed record DaemonStartProgressEntry
{
    /// <summary> Initializes one validated host-visible daemon start progress payload. </summary>
    [JsonConstructor]
    public DaemonStartProgressEntry (
        ProjectFingerprint ProjectFingerprint,
        int TimeoutMilliseconds,
        DaemonEditorMode? EditorMode,
        DaemonStartupBlockedProcessPolicy OnStartupBlocked,
        CommandProgressResult? Result,
        string? StartStatus,
        string? DaemonStatus,
        string? ErrorCode)
    {
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.TimeoutMilliseconds = ContractArgumentGuard.RequireNonNegative(TimeoutMilliseconds, nameof(TimeoutMilliseconds));
        this.EditorMode = EditorMode;
        this.OnStartupBlocked = OnStartupBlocked;
        this.Result = Result;
        this.StartStatus = StartStatus;
        this.DaemonStatus = DaemonStatus;
        this.ErrorCode = ErrorCode;
    }

    public ProjectFingerprint ProjectFingerprint { get; }

    public int TimeoutMilliseconds { get; }

    public DaemonEditorMode? EditorMode { get; }

    public DaemonStartupBlockedProcessPolicy OnStartupBlocked { get; }

    public CommandProgressResult? Result { get; }

    public string? StartStatus { get; }

    public string? DaemonStatus { get; }

    public string? ErrorCode { get; }
}
