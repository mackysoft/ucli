using System.Collections.Concurrent;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Execution;

/// <summary> Implements process-local in-memory lifecycle locks scoped by storage root and project fingerprint. </summary>
internal sealed class InMemoryProjectLifecycleLockProvider : IProjectLifecycleLockProvider
{
    private static readonly ConcurrentDictionary<string, LockState> LocksByFingerprint = new(StringComparer.Ordinal);

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="InMemoryProjectLifecycleLockProvider" /> class. </summary>
    /// <param name="timeProvider"> The time provider used for timeout interpretation. </param>
    public InMemoryProjectLifecycleLockProvider (TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Acquires the lifecycle lock for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="timeout"> The timeout budget used while waiting for lock acquisition. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The async-disposable lock handle that must be disposed to release lock. </returns>
    /// <exception cref="ArgumentException"> Thrown when one argument is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="TimeoutException"> Thrown when lock acquisition exceeds <paramref name="timeout" />. </exception>
    public async ValueTask<IAsyncDisposable> Acquire (
        string storageRoot,
        string projectFingerprint,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(projectFingerprint, out var normalizedProjectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var lockKey = UcliStoragePathResolver.ResolveLifecycleLockPath(
            storageRoot,
            normalizedProjectFingerprint);
        var lockState = LocksByFingerprint.GetOrAdd(
            lockKey,
            static _ => new LockState());
        if (lockState.TryAcquireImmediately())
        {
            return new LockHandle(lockState);
        }

        var waitRegistration = lockState.EnqueueWaiter();
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (waitRegistration.Task.IsCompletedSuccessfully)
                {
                    await waitRegistration.Task.ConfigureAwait(false);
                    return new LockHandle(lockState);
                }

                if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
                {
                    if (lockState.TryRemoveWaiter(waitRegistration))
                    {
                        throw new TimeoutException(
                            $"Timed out while waiting to acquire project lifecycle lock. Timeout={timeout.TotalMilliseconds:0}ms.");
                    }

                    await waitRegistration.Task.ConfigureAwait(false);
                    return new LockHandle(lockState);
                }

                var completedTask = await Task.WhenAny(
                        waitRegistration.Task,
                        TimeProviderDelay.Delay(remainingTimeout, timeProvider, cancellationToken))
                    .ConfigureAwait(false);
                if (completedTask == waitRegistration.Task)
                {
                    await waitRegistration.Task.ConfigureAwait(false);
                    return new LockHandle(lockState);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (lockState.TryRemoveWaiter(waitRegistration))
            {
                throw;
            }

            await waitRegistration.Task.ConfigureAwait(false);
            return new LockHandle(lockState);
        }
    }

    /// <summary> Releases one semaphore lock when disposed. </summary>
    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly LockState lockState;

        private bool disposed;

        public LockHandle (LockState lockState)
        {
            this.lockState = lockState ?? throw new ArgumentNullException(nameof(lockState));
        }

        /// <summary> Releases the held semaphore lock once. </summary>
        /// <returns> A completed task. </returns>
        public ValueTask DisposeAsync ()
        {
            if (disposed)
            {
                return ValueTask.CompletedTask;
            }

            disposed = true;
            lockState.Release();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class LockState
    {
        private readonly object syncRoot = new();

        private readonly LinkedList<WaitRegistration> waiters = new();

        private bool isHeld;

        public bool TryAcquireImmediately ()
        {
            lock (syncRoot)
            {
                if (isHeld)
                {
                    return false;
                }

                isHeld = true;
                return true;
            }
        }

        public WaitRegistration EnqueueWaiter ()
        {
            lock (syncRoot)
            {
                if (!isHeld)
                {
                    isHeld = true;
                    return WaitRegistration.CreateGranted();
                }

                var waitRegistration = new WaitRegistration();
                waitRegistration.Attach(waiters.AddLast(waitRegistration));
                return waitRegistration;
            }
        }

        public bool TryRemoveWaiter (WaitRegistration waitRegistration)
        {
            ArgumentNullException.ThrowIfNull(waitRegistration);

            lock (syncRoot)
            {
                if (!waitRegistration.TryRemove())
                {
                    return false;
                }

                waiters.Remove(waitRegistration.Detach());
                return true;
            }
        }

        public void Release ()
        {
            WaitRegistration? grantedWaiter = null;
            lock (syncRoot)
            {
                while (waiters.First != null)
                {
                    grantedWaiter = waiters.First.Value;
                    waiters.RemoveFirst();
                    grantedWaiter.Detach();
                    if (grantedWaiter.TryGrant())
                    {
                        break;
                    }

                    grantedWaiter = null;
                }

                if (grantedWaiter == null)
                {
                    isHeld = false;
                    return;
                }
            }

            grantedWaiter.SetAcquired();
        }
    }

    private sealed class WaitRegistration
    {
        private readonly TaskCompletionSource completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private LinkedListNode<WaitRegistration>? node;

        private WaitRegistrationState state;

        public Task Task => completionSource.Task;

        public static WaitRegistration CreateGranted ()
        {
            var waitRegistration = new WaitRegistration();
            waitRegistration.state = WaitRegistrationState.Granted;
            waitRegistration.completionSource.TrySetResult();
            return waitRegistration;
        }

        public void Attach (LinkedListNode<WaitRegistration> node)
        {
            this.node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public LinkedListNode<WaitRegistration> Detach ()
        {
            var attachedNode = node ?? throw new InvalidOperationException("Wait registration is not attached.");
            node = null;
            return attachedNode;
        }

        public bool TryGrant ()
        {
            if (state != WaitRegistrationState.Queued)
            {
                return false;
            }

            state = WaitRegistrationState.Granted;
            return true;
        }

        public bool TryRemove ()
        {
            if (state != WaitRegistrationState.Queued)
            {
                return false;
            }

            state = WaitRegistrationState.Removed;
            return true;
        }

        public void SetAcquired ()
        {
            completionSource.TrySetResult();
        }
    }

    private enum WaitRegistrationState
    {
        Queued,
        Granted,
        Removed,
    }
}