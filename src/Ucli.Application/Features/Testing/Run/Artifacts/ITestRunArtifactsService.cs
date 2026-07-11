using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

/// <summary> Prepares and completes test-run artifact sessions. </summary>
internal interface ITestRunArtifactsService
{
    /// <summary> Prepares one run-scoped artifact directory and initial metadata file. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the preparation result. </returns>
    ValueTask<ArtifactsPreparationResult> PrepareAsync (
        ResolvedTestRunConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary> Completes one run-scoped artifact session by attempting to remove interrupted oneshot editor-log exports and updating completion metadata. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <param name="target"> The execution target held fixed for this test run; <see cref="UnityExecutionTarget.Oneshot" /> enables interrupted editor-log export cleanup. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the completion result. </returns>
    ValueTask<ArtifactsCompletionResult> CompleteAsync (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        UnityExecutionTarget target,
        CancellationToken cancellationToken = default);
}
