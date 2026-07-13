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
        string? EditorMode,
        string OnStartupBlocked,
        string? Result,
        string? StartStatus,
        string? DaemonStatus,
        string? ErrorCode)
    {
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.TimeoutMilliseconds = ContractArgumentGuard.RequireNonNegative(TimeoutMilliseconds, nameof(TimeoutMilliseconds));
        this.EditorMode = EditorMode;
        this.OnStartupBlocked = ContractArgumentGuard.RequireValue(OnStartupBlocked, nameof(OnStartupBlocked));
        this.Result = Result;
        this.StartStatus = StartStatus;
        this.DaemonStatus = DaemonStatus;
        this.ErrorCode = ErrorCode;
    }

    public ProjectFingerprint ProjectFingerprint { get; }

    public int TimeoutMilliseconds { get; }

    public string? EditorMode { get; }

    public string OnStartupBlocked { get; }

    public string? Result { get; }

    public string? StartStatus { get; }

    public string? DaemonStatus { get; }

    public string? ErrorCode { get; }
}
