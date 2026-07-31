using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Defines the common immutable facts of one finalized Lifecycle Execution terminal record.
/// </summary>
public abstract record LifecycleExecutionTerminalRecord
{
    /// <summary> Initializes the common terminal facts retained by every lifecycle action. </summary>
    protected LifecycleExecutionTerminalRecord (
        Guid executionId,
        Sha256Digest definitionDigest,
        UnityProjectIdentity project,
        LifecycleExecutionHostRegistration host,
        UnityEditorGenerationSnapshot startedGeneration,
        UnityEditorGenerationSnapshot? terminalGeneration,
        DateTimeOffset deadlineUtc,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        LifecycleExecutionTerminalReason terminalReason,
        ExecutionApplicationState applicationState,
        Verdict? verdict,
        IReadOnlyList<ArtifactRef> artifactRefs)
    {
        ExecutionId = ContractArgumentGuard.RequireNonEmptyGuid(executionId, nameof(executionId));
        DefinitionDigest = definitionDigest ?? throw new ArgumentNullException(nameof(definitionDigest));
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Host = host ?? throw new ArgumentNullException(nameof(host));
        StartedGeneration = startedGeneration ?? throw new ArgumentNullException(nameof(startedGeneration));
        TerminalGeneration = terminalGeneration;

        StartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(startedAtUtc, nameof(startedAtUtc));
        DeadlineUtc = ContractArgumentGuard.RequireUtcTimestamp(deadlineUtc, nameof(deadlineUtc));
        if (DeadlineUtc <= StartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deadlineUtc),
                deadlineUtc,
                "Lifecycle Execution deadline must follow its start time.");
        }

        CompletedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(completedAtUtc, nameof(completedAtUtc));
        if (CompletedAtUtc < StartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedAtUtc),
                completedAtUtc,
                "Terminal completion must not precede the execution start.");
        }

        if (!TextVocabulary.IsDefined(terminalReason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(terminalReason),
                terminalReason,
                "Lifecycle Execution terminal reason must be defined.");
        }
        if (terminalReason == LifecycleExecutionTerminalReason.DeadlineExceeded
            && CompletedAtUtc < DeadlineUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedAtUtc),
                completedAtUtc,
                "A deadline-exceeded Lifecycle Execution cannot complete before its deadline.");
        }
        if (terminalReason != LifecycleExecutionTerminalReason.DeadlineExceeded
            && CompletedAtUtc >= DeadlineUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedAtUtc),
                completedAtUtc,
                "A Lifecycle Execution terminal candidate fixed at or after its deadline must use the deadline-exceeded reason.");
        }

        if (!TextVocabulary.IsDefined(applicationState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Lifecycle Execution application state must be defined.");
        }
        if (verdict.HasValue && !TextVocabulary.IsDefined(verdict.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(verdict),
                verdict,
                "Lifecycle Execution verdict must be defined.");
        }

        var actualArtifactRefs =
            ContractArgumentGuard.RequireItems(artifactRefs, nameof(artifactRefs));
        if (terminalReason == LifecycleExecutionTerminalReason.UnityExited)
        {
            if (terminalGeneration != null)
            {
                throw new ArgumentException(
                    "A Unity-exited Lifecycle Execution cannot attribute a terminal Editor generation.",
                    nameof(terminalGeneration));
            }
            if (applicationState
                is not ExecutionApplicationState.NotApplied
                and not ExecutionApplicationState.Indeterminate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(applicationState),
                    applicationState,
                    "A Unity-exited Lifecycle Execution can retain only confirmed non-application or an indeterminate application state.");
            }
            if (verdict.HasValue)
            {
                throw new ArgumentException(
                    "A Unity-exited Lifecycle Execution cannot publish an action verdict.",
                    nameof(verdict));
            }
            if (actualArtifactRefs.Count != 0)
            {
                throw new ArgumentException(
                    "A Unity-exited Lifecycle Execution cannot attribute action artifacts.",
                    nameof(artifactRefs));
            }
        }

        TerminalReason = terminalReason;
        ApplicationState = applicationState;
        Verdict = verdict;
        ArtifactRefs = actualArtifactRefs;
    }

    /// <summary> Gets the action discriminator serialized as <c>executionKind</c>. </summary>
    [JsonIgnore]
    public LifecycleExecutionKind ExecutionKind => ExecutionKindCore;

    private protected abstract LifecycleExecutionKind ExecutionKindCore { get; }

    /// <summary> Gets the non-empty execution identifier shared with the corresponding reference. </summary>
    [JsonInclude]
    [JsonRequired]
    public Guid ExecutionId { get; private init; }

    /// <summary> Gets the digest of the immutable action definition. </summary>
    [JsonInclude]
    [JsonRequired]
    public Sha256Digest DefinitionDigest { get; private init; }

    /// <summary> Gets the project identity fixed before the action began. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityProjectIdentity Project { get; private init; }

    /// <summary> Gets the fixed Editor host and its first and terminal endpoint registrations. </summary>
    [JsonInclude]
    [JsonRequired]
    public LifecycleExecutionHostRegistration Host { get; private init; }

    /// <summary> Gets the complete Editor generation fixed before the action began. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityEditorGenerationSnapshot StartedGeneration { get; private init; }

    /// <summary> Gets the terminal Editor generation when it could be verified on the same host. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityEditorGenerationSnapshot? TerminalGeneration { get; private init; }

    /// <summary> Gets the immutable execution deadline. </summary>
    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset DeadlineUtc { get; private init; }

    /// <summary> Gets the execution registration time. </summary>
    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset StartedAtUtc { get; private init; }

    /// <summary> Gets the time at which the terminal candidate was fixed. </summary>
    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset CompletedAtUtc { get; private init; }

    /// <summary> Gets the typed reason for terminalization. </summary>
    [JsonInclude]
    [JsonRequired]
    public LifecycleExecutionTerminalReason TerminalReason { get; private init; }

    /// <summary> Gets the action-specific application state confirmed at terminalization. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionApplicationState ApplicationState { get; private init; }

    /// <summary> Gets the action-owned verdict, or <see langword="null" /> when none was established. </summary>
    [JsonInclude]
    [JsonRequired]
    public Verdict? Verdict { get; private init; }

    /// <summary> Gets action-owned artifacts that were published and verified before this record. </summary>
    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<ArtifactRef> ArtifactRefs { get; private init; }

    private protected void ValidateActionResult (
        LifecycleExecutionKind expectedKind,
        object? result,
        string resultParameterName,
        bool allowsVerdict)
    {
        var expectedDigest = LifecycleExecutionDefinitionDigest.Calculate(
            new LifecycleExecutionDefinition(expectedKind));
        if (DefinitionDigest != expectedDigest)
        {
            throw new ArgumentException(
                "Terminal record definition digest does not match its execution kind.",
                nameof(DefinitionDigest));
        }

        if (TerminalReason == LifecycleExecutionTerminalReason.UnityExited
            && result != null)
        {
            throw new ArgumentException(
                "A Unity-exited Lifecycle Execution cannot retain an action result.",
                resultParameterName);
        }

        if (TerminalReason == LifecycleExecutionTerminalReason.Completed)
        {
            if (result == null)
            {
                throw new ArgumentNullException(
                    resultParameterName,
                    "A completed Lifecycle Execution must retain its typed result.");
            }
            if (TerminalGeneration == null)
            {
                throw new ArgumentNullException(
                    nameof(TerminalGeneration),
                    "A completed Lifecycle Execution must retain its terminal Editor generation.");
            }
        }

        if (!allowsVerdict && Verdict.HasValue)
        {
            throw new ArgumentException(
                "This Lifecycle Execution action does not own a verdict.",
                nameof(Verdict));
        }

        if (TerminalReason != LifecycleExecutionTerminalReason.Completed
            && Verdict.HasValue)
        {
            throw new ArgumentException(
                "A Lifecycle Execution that did not complete its typed result contract cannot publish a verdict.",
                nameof(Verdict));
        }

        if (allowsVerdict
            && TerminalReason == LifecycleExecutionTerminalReason.Completed
            && !Verdict.HasValue)
        {
            throw new ArgumentNullException(
                nameof(Verdict),
                "A completed Lifecycle Execution that owns a verdict must publish it from its typed result.");
        }
    }

    private protected void ValidateResultSuccess (
        bool resultIsSuccessful,
        string resultParameterName)
    {
        if (TerminalReason == LifecycleExecutionTerminalReason.Completed
            && !resultIsSuccessful)
        {
            throw new ArgumentException(
                "The completed terminal reason requires a successful action result.",
                resultParameterName);
        }
        if (resultIsSuccessful
            && TerminalReason
                is not LifecycleExecutionTerminalReason.Completed
                and not LifecycleExecutionTerminalReason.DeadlineExceeded)
        {
            throw new ArgumentException(
                "A successful action result may remain non-completed only when the durable deadline won terminal convergence.",
                resultParameterName);
        }
    }

    private protected void ValidateTerminalGeneration (
        UnityEditorGenerationSnapshot resultGeneration,
        string terminalGenerationParameterName)
    {
        if (TerminalGeneration != resultGeneration)
        {
            throw new ArgumentException(
                "Lifecycle Execution terminal generation must match the final generation determined by its typed result.",
                terminalGenerationParameterName);
        }
    }

    private protected void ValidatePlayApplicationState (
        PlayLifecycleTransitionResult result,
        string resultParameterName)
    {
        if (result.OutcomeApplicationState != ApplicationState)
        {
            throw new ArgumentException(
                "The Play Mode outcome and terminal record must report the same application state.",
                result.IsSuccessful
                    ? nameof(ApplicationState)
                    : resultParameterName);
        }
    }

    private protected void ValidateObservationProject (
        UnityEditorObservation observation,
        string parameterName)
    {
        if (observation.ProjectFingerprint != Project.ProjectFingerprint)
        {
            throw new ArgumentException(
                "Lifecycle observation project must match the terminal record project.",
                parameterName);
        }
    }
}
