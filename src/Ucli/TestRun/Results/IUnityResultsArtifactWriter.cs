using MackySoft.Ucli.TestRun.Artifacts;

namespace MackySoft.Ucli.TestRun.Results;

/// <summary> Writes normalized Unity test result JSON artifacts from parsed data. </summary>
internal interface IUnityResultsArtifactWriter
{
    /// <summary> Writes one results session artifacts from parsed XML values. </summary>
    /// <param name="session"> The run artifacts session. </param>
    /// <param name="parseResult"> The parsed XML result values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that completes when writing is finished. </returns>
    ValueTask Write (
        ArtifactsSession session,
        UnityResultsXmlParseResult parseResult,
        CancellationToken cancellationToken = default);
}