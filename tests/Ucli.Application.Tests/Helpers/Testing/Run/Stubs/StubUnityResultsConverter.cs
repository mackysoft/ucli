using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubUnityResultsConverter : IUnityResultsConverter
{
    private readonly Func<ArtifactsSession, ValueTask<UnityResultsConversionResult>> convert;

    public StubUnityResultsConverter (Func<ArtifactsSession, ValueTask<UnityResultsConversionResult>> convert)
    {
        this.convert = convert;
    }

    public bool? LastAllowEmptyTestRun { get; private set; }

    public ValueTask<UnityResultsConversionResult> ConvertAsync (
        ArtifactsSession session,
        bool allowEmptyTestRun,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastAllowEmptyTestRun = allowEmptyTestRun;
        return convert(session);
    }
}
