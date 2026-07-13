using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.started</c> stream payload. </summary>
public sealed record CompileStartedEntry
{
    /// <summary> Initializes one validated <c>compile.started</c> stream payload. </summary>
    [JsonConstructor]
    public CompileStartedEntry (
        string RunId,
        ProjectFingerprint ProjectFingerprint,
        string RequestedMode,
        string ResolvedMode,
        string SessionKind,
        int TimeoutMilliseconds)
    {
        this.RunId = ContractArgumentGuard.RequireValue(RunId, nameof(RunId));
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.RequestedMode = ContractArgumentGuard.RequireValue(RequestedMode, nameof(RequestedMode));
        this.ResolvedMode = ContractArgumentGuard.RequireValue(ResolvedMode, nameof(ResolvedMode));
        this.SessionKind = ContractArgumentGuard.RequireValue(SessionKind, nameof(SessionKind));
        this.TimeoutMilliseconds = ContractArgumentGuard.RequireNonNegative(TimeoutMilliseconds, nameof(TimeoutMilliseconds));
    }

    public string RunId { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public string RequestedMode { get; }

    public string ResolvedMode { get; }

    public string SessionKind { get; }

    public int TimeoutMilliseconds { get; }
}
