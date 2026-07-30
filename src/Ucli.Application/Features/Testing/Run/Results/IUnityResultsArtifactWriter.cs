using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Results;

/// <summary> Writes normalized Unity test result JSON artifacts from parsed data. </summary>
internal interface IUnityResultsArtifactWriter
{
    /// <summary> Writes one results session from the complete normalized verdict evaluation. </summary>
    /// <param name="session"> The run artifacts session. </param>
    /// <param name="verdictEvaluation"> The normalized result, policy input, and verdict derived from that result. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that completes when writing is finished. </returns>
    ValueTask WriteAsync (
        ArtifactsSession session,
        TestRunVerdictEvaluation verdictEvaluation,
        CancellationToken cancellationToken = default);
}
