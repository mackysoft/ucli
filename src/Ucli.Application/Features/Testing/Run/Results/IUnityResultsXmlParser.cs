using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Results;

/// <summary> Parses Unity test results XML into normalized intermediate models. </summary>
internal interface IUnityResultsXmlParser
{
    /// <summary> Parses one Unity test results XML file. </summary>
    /// <param name="resultsXmlPath"> The results XML path. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to parsed XML result values. </returns>
    ValueTask<UnityResultsXmlParseResult> ParseAsync (
        AbsolutePath resultsXmlPath,
        CancellationToken cancellationToken = default);
}
