using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Represents the immutable terminal record of one Play Mode entry Lifecycle Execution. </summary>
public sealed record PlayEnterLifecycleExecutionTerminalRecord : LifecycleExecutionTerminalRecord
{
    /// <summary> Initializes one Play Mode entry terminal record. </summary>
    [JsonConstructor]
    public PlayEnterLifecycleExecutionTerminalRecord (
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
        PlayEnterLifecycleTransitionResult? result,
        Verdict? verdict,
        IReadOnlyList<ArtifactRef> artifactRefs)
        : base(
            executionId,
            definitionDigest,
            project,
            host,
            startedGeneration,
            terminalGeneration,
            deadlineUtc,
            startedAtUtc,
            completedAtUtc,
            terminalReason,
            applicationState,
            verdict,
            artifactRefs)
    {
        ValidateActionResult(
            LifecycleExecutionKind.PlayEnter,
            result,
            nameof(result),
            allowsVerdict: false);
        if (result != null)
        {
            ValidateResultSuccess(result.IsSuccessful, nameof(result));
            ValidatePlayApplicationState(result, nameof(result));
            ValidateResultProjects(result);
            ValidateTerminalGeneration(
                (result.IsSuccessful ? result.After! : result.Observed!)
                    .State.Generations,
                nameof(terminalGeneration));
        }

        Result = result;
    }

    private protected override LifecycleExecutionKind ExecutionKindCore =>
        LifecycleExecutionKind.PlayEnter;

    /// <summary> Gets the typed Play Mode entry result when it could be established. </summary>
    [JsonInclude]
    [JsonRequired]
    public PlayEnterLifecycleTransitionResult? Result { get; private init; }

    private void ValidateResultProjects (
        PlayEnterLifecycleTransitionResult result)
    {
        ValidateObservationProject(result.Before, nameof(result));
        if (result.After != null)
        {
            ValidateObservationProject(result.After, nameof(result));
        }
        if (result.Observed != null)
        {
            ValidateObservationProject(result.Observed, nameof(result));
        }
    }
}
