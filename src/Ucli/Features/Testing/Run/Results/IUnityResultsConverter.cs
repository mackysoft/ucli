using MackySoft.Ucli.Features.Testing.Run.Artifacts;

namespace MackySoft.Ucli.Features.Testing.Run.Results;

/// <summary> Converts Unity test results XML into normalized JSON artifacts. </summary>
internal interface IUnityResultsConverter
{
    /// <summary> Converts one artifacts session results XML into normalized JSON artifacts. </summary>
    /// <param name="session"> The run artifacts session. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the conversion result. </returns>
    ValueTask<UnityResultsConversionResult> Convert (
        ArtifactsSession session,
        CancellationToken cancellationToken = default);
}