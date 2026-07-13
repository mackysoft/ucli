using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.started</c> stream payload. </summary>
public sealed record CompileStartedEntry
{
    /// <summary> Initializes one validated <c>compile.started</c> stream payload. </summary>
    [JsonConstructor]
    public CompileStartedEntry (
        Guid RunId,
        ProjectFingerprint ProjectFingerprint,
        string RequestedMode,
        string ResolvedMode,
        string SessionKind,
        int TimeoutMilliseconds)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.RequestedMode = ContractArgumentGuard.RequireValue(RequestedMode, nameof(RequestedMode));
        this.ResolvedMode = ContractArgumentGuard.RequireValue(ResolvedMode, nameof(ResolvedMode));
        this.SessionKind = ContractArgumentGuard.RequireValue(SessionKind, nameof(SessionKind));
        this.TimeoutMilliseconds = ContractArgumentGuard.RequireNonNegative(TimeoutMilliseconds, nameof(TimeoutMilliseconds));
    }

    public Guid RunId { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public string RequestedMode { get; }

    public string ResolvedMode { get; }

    public string SessionKind { get; }

    public int TimeoutMilliseconds { get; }
}
