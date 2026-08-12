using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Tests;

internal sealed class RecordingRefreshService : IRefreshService
{
    private readonly List<Guid> requestIds = [];

    private readonly Func<Guid, LifecycleExecutionStartInvocation, bool, CancellationToken, ValueTask<RefreshExecutionResult>> handler;

    public RecordingRefreshService (Func<Guid, LifecycleExecutionStartInvocation, bool, CancellationToken, ValueTask<RefreshExecutionResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public IReadOnlyList<Guid> RequestIds => requestIds;

    public List<RefreshServiceInvocation> Invocations { get; } = [];

    public ValueTask<RefreshExecutionResult> StartAsync (
        Guid requestId,
        LifecycleExecutionStartInvocation invocation,
        bool failFast,
        CancellationToken cancellationToken = default)
    {
        requestIds.Add(requestId);
        Invocations.Add(new RefreshServiceInvocation(requestId, invocation, failFast, cancellationToken));
        return handler(requestId, invocation, failFast, cancellationToken);
    }

    public ValueTask<RefreshExecutionResult> ReconnectAsync (
        Guid requestId,
        LifecycleExecutionReconnectInvocation invocation,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Fixed-context refresh reconnect was not expected.");


    internal sealed record RefreshServiceInvocation (
        Guid RequestId,
        LifecycleExecutionStartInvocation Invocation,
        bool FailFast,
        CancellationToken CancellationToken);
}
