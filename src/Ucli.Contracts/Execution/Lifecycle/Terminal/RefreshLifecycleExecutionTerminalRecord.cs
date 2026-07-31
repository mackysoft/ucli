using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Represents the immutable terminal record of one refresh Lifecycle Execution. </summary>
public sealed record RefreshLifecycleExecutionTerminalRecord : LifecycleExecutionTerminalRecord
{
    /// <summary> Initializes one refresh terminal record. </summary>
    [JsonConstructor]
    public RefreshLifecycleExecutionTerminalRecord (
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
        RefreshLifecycleResult? result,
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
            LifecycleExecutionKind.Refresh,
            result,
            nameof(result),
            allowsVerdict: false);
        if (result != null)
        {
            ValidateObservationProject(result.Lifecycle, nameof(result));
            ValidateTerminalGeneration(
                result.Lifecycle.State.Generations,
                nameof(terminalGeneration));
        }

        Result = result;
    }

    private protected override LifecycleExecutionKind ExecutionKindCore =>
        LifecycleExecutionKind.Refresh;

    /// <summary> Gets the typed refresh result when it could be established. </summary>
    [JsonInclude]
    [JsonRequired]
    public RefreshLifecycleResult? Result { get; private init; }
}
