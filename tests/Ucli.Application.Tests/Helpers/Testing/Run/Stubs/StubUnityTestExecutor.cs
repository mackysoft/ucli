using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubUnityTestExecutor
{
    private readonly Func<ResolvedTestRunConfiguration, ArtifactPaths, TimeSpan, CancellationToken, ValueTask<UnityTestExecutionResult>> execute;

    public StubUnityTestExecutor (
        Func<ResolvedTestRunConfiguration, ArtifactPaths, TimeSpan, CancellationToken, ValueTask<UnityTestExecutionResult>> execute)
    {
        this.execute = execute;
    }

    public ValueTask<UnityTestExecutionResult> ExecuteAsync (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return execute(configuration, artifactPaths, timeout, cancellationToken);
    }
}
