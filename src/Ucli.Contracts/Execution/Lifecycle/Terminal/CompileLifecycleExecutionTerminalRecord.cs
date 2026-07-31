using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Represents the immutable terminal record of one compile Lifecycle Execution. </summary>
public sealed record CompileLifecycleExecutionTerminalRecord : LifecycleExecutionTerminalRecord
{
    /// <summary> Initializes one compile terminal record. </summary>
    [JsonConstructor]
    public CompileLifecycleExecutionTerminalRecord (
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
        CompileLifecycleResult? result,
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
            LifecycleExecutionKind.Compile,
            result,
            nameof(result),
            allowsVerdict: true);
        if (result?.Lifecycle.State is not null)
        {
            ValidateTerminalGeneration(
                result.Lifecycle.State.Generations,
                nameof(terminalGeneration));
        }
        if (terminalReason == LifecycleExecutionTerminalReason.Completed
            && result is not null
            && verdict != CompileLifecycleVerdictPolicy.Evaluate(result))
        {
            throw new ArgumentException(
                "Compile verdict must match the action-owned typed result.",
                nameof(verdict));
        }

        Result = result;
    }

    private protected override LifecycleExecutionKind ExecutionKindCore =>
        LifecycleExecutionKind.Compile;

    /// <summary> Gets the typed compile result when it could be established. </summary>
    [JsonInclude]
    [JsonRequired]
    public CompileLifecycleResult? Result { get; private init; }

}
