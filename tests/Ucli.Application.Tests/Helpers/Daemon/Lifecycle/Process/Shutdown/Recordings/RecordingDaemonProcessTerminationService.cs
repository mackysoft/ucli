using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonProcessTerminationService : IDaemonProcessTerminationService
{
    private readonly List<Invocation> invocations = [];

    public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonSessionStoreOperationResult> EnsureStoppedAsync (
        DaemonProcessTerminationTarget? target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(target, timeout, cancellationToken));
        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        DaemonProcessTerminationTarget? Target,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}
