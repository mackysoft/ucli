using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Tests.Daemon;

internal sealed class StubProjectLifecycleLockProvider : IProjectLifecycleLockProvider
{
    public bool ThrowTimeoutOnAcquire { get; set; }

    public ProjectLifecycleLockRequest? LastRequest { get; private set; }

    public ValueTask<IAsyncDisposable> Acquire (
        ProjectLifecycleLockRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastRequest = request;
        if (ThrowTimeoutOnAcquire)
        {
            throw new TimeoutException("lock timeout");
        }

        return ValueTask.FromResult<IAsyncDisposable>(NoopAsyncDisposable.Instance);
    }

    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        public static readonly NoopAsyncDisposable Instance = new();

        public ValueTask DisposeAsync ()
        {
            return ValueTask.CompletedTask;
        }
    }
}
