using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubTestRunProfileJsonReader : ITestRunProfileJsonReader
{
    private readonly string json;

    public StubTestRunProfileJsonReader (string json)
    {
        this.json = json;
    }

    public ValueTask<TestRunProfileJsonReadResult> ReadTextAsync (
        string profilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(TestRunProfileJsonReadResult.Success(json));
    }
}
