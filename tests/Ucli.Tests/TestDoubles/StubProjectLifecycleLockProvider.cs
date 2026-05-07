using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Ucli.Tests.TestDoubles;

internal sealed class StubProjectLifecycleLockProvider : IProjectLifecycleLockProvider
{
    private readonly Func<ProjectLifecycleLockRequest, TimeSpan, CancellationToken, IAsyncDisposable> acquire;

    public StubProjectLifecycleLockProvider ()
        : this(throwTimeout: false)
    {
    }

    public StubProjectLifecycleLockProvider (bool throwTimeout)
    {
        acquire = (_, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (throwTimeout)
            {
                throw new TimeoutException("lock timeout");
            }

            return NoopAsyncDisposable.Instance;
        };
    }

    public StubProjectLifecycleLockProvider (Func<ProjectLifecycleLockRequest, TimeSpan, CancellationToken, IAsyncDisposable> acquire)
    {
        this.acquire = acquire ?? throw new ArgumentNullException(nameof(acquire));
    }

    public ProjectLifecycleLockRequest? LastRequest { get; private set; }

    public ValueTask<IAsyncDisposable> Acquire (
        ProjectLifecycleLockRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastRequest = request;
        return ValueTask.FromResult(acquire(request, timeout, cancellationToken));
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
