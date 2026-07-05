using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;

namespace MackySoft.Ucli.Tests.Helpers.Testing;

internal sealed class ThrowingUnityResultsArtifactWriter : IUnityResultsArtifactWriter
{
    private readonly Exception exception;

    public ThrowingUnityResultsArtifactWriter (Exception exception)
    {
        this.exception = exception;
    }

    public ValueTask WriteAsync (
        ArtifactsSession session,
        UnityResultsXmlParseResult parseResult,
        CancellationToken cancellationToken = default)
    {
        throw exception;
    }
}
