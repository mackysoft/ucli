using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Ucli.TestSupport;

internal sealed class StubProjectLifecycleLockProvider : IProjectLifecycleLockProvider
{
    private readonly Func<ProjectLifecycleLockRequest, TimeSpan, CancellationToken, IAsyncDisposable>? acquire;
    private readonly bool throwTimeout;

    private readonly List<Invocation> invocations = [];

    public StubProjectLifecycleLockProvider ()
        : this(throwTimeout: false)
    {
    }

    public StubProjectLifecycleLockProvider (bool throwTimeout)
    {
        this.throwTimeout = throwTimeout;
    }

    public StubProjectLifecycleLockProvider (Func<ProjectLifecycleLockRequest, TimeSpan, CancellationToken, IAsyncDisposable> acquire)
    {
        this.acquire = acquire ?? throw new ArgumentNullException(nameof(acquire));
    }

    public bool ThrowTimeoutOnAcquire { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<IAsyncDisposable> AcquireAsync (
        ProjectLifecycleLockRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(request, timeout, cancellationToken));
        if (throwTimeout || ThrowTimeoutOnAcquire)
        {
            throw new TimeoutException("lock timeout");
        }

        return ValueTask.FromResult(acquire?.Invoke(request, timeout, cancellationToken) ?? NoopAsyncDisposable.Instance);
    }

    internal readonly record struct Invocation (
        ProjectLifecycleLockRequest Request,
        TimeSpan Timeout,
        CancellationToken CancellationToken);

    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        public static readonly NoopAsyncDisposable Instance = new();

        public ValueTask DisposeAsync ()
        {
            return ValueTask.CompletedTask;
        }
    }
}
