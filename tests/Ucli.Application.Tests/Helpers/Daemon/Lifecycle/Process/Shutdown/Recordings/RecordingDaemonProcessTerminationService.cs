using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonProcessTerminationService : IDaemonProcessTerminationService
{
    private readonly List<Invocation> invocations = [];

    public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

    public Func<DaemonProcessTerminationTarget?, ExecutionDeadline, CancellationToken, ValueTask<DaemonSessionStoreOperationResult>>? Handler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonSessionStoreOperationResult> EnsureStoppedAsync (
        DaemonProcessTerminationTarget? target,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(target, deadline, cancellationToken));
        if (Handler is not null)
        {
            return Handler(target, deadline, cancellationToken);
        }

        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        DaemonProcessTerminationTarget? Target,
        ExecutionDeadline Deadline,
        CancellationToken CancellationToken);
}
