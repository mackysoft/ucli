using MackySoft.Ucli.Application.Features.Testing.Run.Results;

namespace MackySoft.Ucli.Tests.Helpers.Testing;

internal sealed class StubUnityResultsXmlParser : IUnityResultsXmlParser
{
    private readonly UnityResultsXmlParseResult parseResult;

    public StubUnityResultsXmlParser (UnityResultsXmlParseResult parseResult)
    {
        this.parseResult = parseResult;
    }

    public ValueTask<UnityResultsXmlParseResult> ParseAsync (
        string resultsXmlPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(parseResult);
    }
}
