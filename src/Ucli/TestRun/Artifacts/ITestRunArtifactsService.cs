using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.TestRun.Artifacts;

/// <summary> Prepares and completes test-run artifact sessions. </summary>
internal interface ITestRunArtifactsService
{
    /// <summary> Prepares one run-scoped artifact directory and initial metadata file. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <returns> The preparation result. </returns>
    ArtifactsPreparationResult Prepare (ResolvedTestRunConfiguration configuration);

    /// <summary> Completes one run-scoped artifact session by updating metadata completion values. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="session"> The prepared artifacts session. </param>
    /// <returns> The completion result. </returns>
    ArtifactsCompletionResult Complete (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session);
}