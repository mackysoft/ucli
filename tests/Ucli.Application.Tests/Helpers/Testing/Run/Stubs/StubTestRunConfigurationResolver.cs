using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubTestRunConfigurationResolver : ITestRunConfigurationResolver
{
    private readonly TestRunConfigurationResolutionResult result;

    public StubTestRunConfigurationResolver (TestRunConfigurationResolutionResult result)
    {
        this.result = result;
    }

    public ValueTask<TestRunConfigurationResolutionResult> ResolveAsync (
        TestRunConfigurationRequest input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(result);
    }
}
