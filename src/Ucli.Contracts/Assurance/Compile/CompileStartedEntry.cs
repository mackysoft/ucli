using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.started</c> stream payload. </summary>
public sealed record CompileStartedEntry
{
    /// <summary> Initializes one validated <c>compile.started</c> stream payload. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an execution mode or <paramref name="SessionKind" /> is undefined, or <paramref name="TimeoutMilliseconds" /> is negative.
    /// </exception>
    [JsonConstructor]
    public CompileStartedEntry (
        Guid RunId,
        ProjectFingerprint ProjectFingerprint,
        AssuranceRequestedExecutionMode RequestedMode,
        AssuranceResolvedExecutionMode ResolvedMode,
        AssuranceSessionKind SessionKind,
        int TimeoutMilliseconds)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        if (!TextVocabulary.IsDefined(RequestedMode))
        {
            throw new ArgumentOutOfRangeException(nameof(RequestedMode), RequestedMode, "Requested execution mode must be defined by the assurance contract.");
        }

        if (!TextVocabulary.IsDefined(ResolvedMode))
        {
            throw new ArgumentOutOfRangeException(nameof(ResolvedMode), ResolvedMode, "Resolved execution mode must be defined by the assurance contract.");
        }

        this.RequestedMode = RequestedMode;
        this.ResolvedMode = ResolvedMode;
        if (!TextVocabulary.IsDefined(SessionKind))
        {
            throw new ArgumentOutOfRangeException(nameof(SessionKind), SessionKind, "Session kind must be defined by the assurance contract.");
        }

        this.SessionKind = SessionKind;
        this.TimeoutMilliseconds = ContractArgumentGuard.RequireNonNegative(TimeoutMilliseconds, nameof(TimeoutMilliseconds));
    }

    /// <summary> Gets the non-empty compile run identifier. </summary>
    public Guid RunId { get; }

    /// <summary> Gets the project fingerprint observed by the compile run. </summary>
    public ProjectFingerprint ProjectFingerprint { get; }

    /// <summary> Gets the requested Unity execution mode. </summary>
    public AssuranceRequestedExecutionMode RequestedMode { get; }

    /// <summary> Gets the resolved Unity execution mode. </summary>
    public AssuranceResolvedExecutionMode ResolvedMode { get; }

    /// <summary> Gets the session kind that handled the compile run. </summary>
    public AssuranceSessionKind SessionKind { get; }

    /// <summary> Gets the timeout applied to the compile run, in milliseconds. </summary>
    public int TimeoutMilliseconds { get; }
}
