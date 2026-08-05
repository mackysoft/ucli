using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;

namespace MackySoft.Tests;

internal sealed class StubDaemonCleanupService : IDaemonCleanupService
{
    private readonly List<Invocation> invocations = [];

    public StubDaemonCleanupService (DaemonCleanupExecutionResult result)
    {
        Result = result;
    }

    public DaemonCleanupExecutionResult Result { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonCleanupExecutionResult> CleanupAsync (
        AbsolutePath? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(projectPath, timeoutMilliseconds, cancellationToken));
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        AbsolutePath? ProjectPath,
        int? TimeoutMilliseconds,
        CancellationToken CancellationToken);
}
