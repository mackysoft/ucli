using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonTestRunClient
{
    private readonly Func<ResolvedTestRunConfiguration, ArtifactPaths, TimeSpan, bool, CancellationToken, ValueTask<UnityTestExecutionResult>> execute;
    private readonly List<Invocation> invocations = [];

    public RecordingDaemonTestRunClient (Func<ResolvedTestRunConfiguration, ArtifactPaths, TimeSpan, bool, CancellationToken, ValueTask<UnityTestExecutionResult>> execute)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<UnityTestExecutionResult> ExecuteAsync (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths,
        TimeSpan timeout,
        bool failFast,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(configuration, artifactPaths, timeout, failFast, cancellationToken));
        return execute(configuration, artifactPaths, timeout, failFast, cancellationToken);
    }

    internal readonly record struct Invocation (
        ResolvedTestRunConfiguration Configuration,
        ArtifactPaths ArtifactPaths,
        TimeSpan Timeout,
        bool FailFast,
        CancellationToken CancellationToken);
}
