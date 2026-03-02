using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.TestRun.Artifacts;

/// <summary> Prepares and completes test-run artifact sessions. </summary>
internal interface ITestRunArtifactsService
{
    /// <summary> Prepares one run-scoped artifact directory and initial metadata file. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the preparation result. </returns>
    ValueTask<ArtifactsPreparationResult> Prepare (
        ResolvedTestRunConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary> Completes one run-scoped artifact session by updating metadata completion values. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the completion result. </returns>
    ValueTask<ArtifactsCompletionResult> Complete (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        CancellationToken cancellationToken = default);
}