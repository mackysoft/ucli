using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubBuildProfileFileReader : IBuildProfileFileReader
{
    private readonly BuildProfileFileReadResult result;

    public StubBuildProfileFileReader (BuildProfileFileReadResult result)
    {
        this.result = result;
    }

    public ValueTask<BuildProfileFileReadResult> ReadAsync (
        string profilePath,
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(result);
    }
}
