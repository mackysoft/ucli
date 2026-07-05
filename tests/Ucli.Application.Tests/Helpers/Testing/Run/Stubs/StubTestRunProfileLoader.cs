using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubTestRunProfileLoader : ITestRunProfileLoader
{
    private readonly TestRunProfileLoadResult result;

    public StubTestRunProfileLoader (TestRunProfileLoadResult result)
    {
        this.result = result;
    }

    public ValueTask<TestRunProfileLoadResult> LoadAsync (
        string profilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(result);
    }
}
