using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements unix-domain-socket transport accept loop for Unity IPC server. </summary>
    internal sealed class UnixDomainSocketUnityIpcTransportListener :
        IUnityIpcTransportListener,
        IUnityIpcTransportRunReservation
    {
        private const string EndpointOwnershipLockDirectoryPrefix = "ucli-ipc-lock-";

        private static readonly object EndpointOwnershipSyncRoot = new object();

        private static readonly Dictionary<string, EndpointOwnershipState> ActiveEndpointOwners =
            new Dictionary<string, EndpointOwnershipState>(StringComparer.Ordinal);

        private static readonly SemaphoreSlim EndpointOwnershipClaimGate = new SemaphoreSlim(1, 1);

        private static readonly TimeSpan EndpointOwnershipAcquireTimeout = TimeSpan.FromSeconds(1);

        private readonly object syncRoot = new object();

        private readonly Dictionary<CancellationToken, RunReservation> runReservations =
            new Dictionary<CancellationToken, RunReservation>();

        private readonly IDaemonLogger daemonLogger;

        private readonly int maximumActiveConnections;

        private readonly TimeSpan connectionDrainTimeout;

        private Socket activeListenerSocket;

        private UnityIpcTransportConnectionGroup activeConnectionGroup;

        private EndpointOwnershipLease activeEndpointOwnershipLease;

        /// <summary> Initializes a new instance of the <see cref="UnixDomainSocketUnityIpcTransportListener" /> class. </summary>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        /// <param name="maximumActiveConnections"> The maximum number of accepted connections that may be handled concurrently. </param>
        /// <param name="connectionDrainTimeout"> The maximum time allowed for active connections to finish during listener shutdown. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonLogger" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when a numeric limit is not positive. </exception>
        public UnixDomainSocketUnityIpcTransportListener (
            IDaemonLogger daemonLogger,
            int maximumActiveConnections,
            TimeSpan connectionDrainTimeout)
        {
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
            if (maximumActiveConnections <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumActiveConnections),
                    maximumActiveConnections,
                    "Maximum active connections must be greater than zero.");
            }

            if (connectionDrainTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(connectionDrainTimeout),
                    connectionDrainTimeout,
                    "Connection drain timeout must be greater than zero.");
            }

            this.maximumActiveConnections = maximumActiveConnections;
            this.connectionDrainTimeout = connectionDrainTimeout;
        }

        /// <summary> Gets transport kind handled by this listener. </summary>
        public IpcTransportKind TransportKind => IpcTransportKind.UnixDomainSocket;

        /// <summary> Runs transport-specific accept loop until cancellation is requested. </summary>
        /// <param name="address"> The unix-domain-socket path value. </param>
        /// <param name="connectionHandler"> The connection handler dependency. </param>
        /// <param name="cancellationToken"> The cancellation token for listener lifecycle. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="address" /> is empty. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="connectionHandler" /> is <see langword="null" />. </exception>
        public async Task RunAsync (
            string address,
            IUnityIpcConnectionHandler connectionHandler,
            Action onStarted,
            Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
            CancellationToken cancellationToken)
        {
            var runReservation = ClaimRunReservation(cancellationToken);
            try
            {
                if (string.IsNullOrWhiteSpace(address))
                {
                    throw new ArgumentException("Socket address must not be empty or whitespace.", nameof(address));
                }

                if (connectionHandler == null)
                {
                    throw new ArgumentNullException(nameof(connectionHandler));
                }

                if (onStarted == null)
                {
                    throw new ArgumentNullException(nameof(onStarted));
                }

                if (onConnectionCompleted == null)
                {
                    throw new ArgumentNullException(nameof(onConnectionCompleted));
                }

                UnixSocketPathUtilities.ValidateSocketPathLength(address, nameof(address));
                cancellationToken.ThrowIfCancellationRequested();
                if (runReservation.IsClosed)
                {
                    return;
                }

                // Native socket and filesystem operations must not capture or block Unity's main-thread context.
                await Task.Run(() => RunCoreAsync(
                        address,
                        connectionHandler,
                        onStarted,
                        onConnectionCompleted,
                        runReservation,
                        cancellationToken))
                    .ConfigureAwait(false);
            }
            finally
            {
                RemoveRunReservation(runReservation);
            }
        }

        private async Task RunCoreAsync (
            string address,
            IUnityIpcConnectionHandler connectionHandler,
            Action onStarted,
            Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
            RunReservation runReservation,
            CancellationToken cancellationToken)
        {
            if (runReservation.IsClosed)
            {
                return;
            }

            var accessBoundary = new UnixSocketAccessBoundary(address, UcliIpcEndpointNames.DaemonAddressPrefix);
            using var endpointOwnershipLease = await ClaimEndpointOwnershipAsync(
                    address,
                    accessBoundary,
                    runReservation,
                    cancellationToken)
                .ConfigureAwait(false);
            using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var connectionGroup = new UnityIpcTransportConnectionGroup(
                daemonLogger,
                maximumActiveConnections);
            CancellationTokenRegistration cancellationRegistration = default;

            try
            {
                lock (syncRoot)
                {
                    if (runReservation.IsClosed)
                    {
                        return;
                    }

                    if (activeListenerSocket != null)
                    {
                        throw new InvalidOperationException(
                            "The Unix domain socket listener already has an active RunAsync generation.");
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    accessBoundary.PrepareForBind();
                    listener.Bind(new UnixDomainSocketEndPoint(address));
                    accessBoundary.HardenBoundSocket();
                    listener.Listen(8);
                    activeEndpointOwnershipLease = endpointOwnershipLease;
                    activeListenerSocket = listener;
                    activeConnectionGroup = connectionGroup;
                    RemoveRunReservationWithoutLock(runReservation);
                }

                cancellationRegistration = cancellationToken.Register(
                    () => _ = Task.Run(() => DisposeListenerSocket(listener)));
                cancellationToken.ThrowIfCancellationRequested();
                onStarted();

                while (!cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var acceptedSocket = await listener.AcceptAsync().ConfigureAwait(false);
                        var acceptedConnection = new AcceptedUnixDomainSocketConnection(acceptedSocket);
                        _ = connectionGroup.TryStart(
                            acceptedConnection,
                            () => connectionHandler.HandleAsync(acceptedConnection.Stream, cancellationToken),
                            onConnectionCompleted,
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception) when (cancellationToken.IsCancellationRequested && exception is ObjectDisposedException or SocketException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return;
                    }
                    catch (Exception exception) when (!cancellationToken.IsCancellationRequested && (exception is IOException or InvalidDataException or SocketException))
                    {
                        // NOTE: Probe callers may close timed-out sockets while Unity is busy on the main thread.
                        // Emitting these expected connection-local failures to the Unity console can make recovery slower.
                    }
                }
            }
            finally
            {
                cancellationRegistration.Dispose();
                connectionGroup.Release();
                try
                {
                    await connectionGroup.WaitForCompletionAsync(connectionDrainTimeout).ConfigureAwait(false);
                }
                finally
                {
                    lock (syncRoot)
                    {
                        if (ReferenceEquals(activeListenerSocket, listener))
                        {
                            activeListenerSocket = null;
                        }

                        if (ReferenceEquals(activeConnectionGroup, connectionGroup))
                        {
                            activeConnectionGroup = null;
                        }

                        if (ReferenceEquals(activeEndpointOwnershipLease, endpointOwnershipLease))
                        {
                            activeEndpointOwnershipLease = null;
                        }
                    }
                }
            }
        }

        /// <summary> Marks the active listener generation released and initiates non-blocking transport-handle cleanup. </summary>
        public void Release ()
        {
            Socket listenerSocket;
            UnityIpcTransportConnectionGroup connectionGroup;
            lock (syncRoot)
            {
                foreach (var runReservation in runReservations.Values)
                {
                    runReservation.Close();
                }

                listenerSocket = activeListenerSocket;
                if (listenerSocket != null)
                {
                    activeListenerSocket = null;
                    activeEndpointOwnershipLease?.AllowSameProcessSuccessor();
                }

                connectionGroup = activeConnectionGroup;
            }

            if (listenerSocket != null)
            {
                _ = Task.Run(() => DisposeListenerSocket(listenerSocket));
            }

            connectionGroup?.Release();
        }

        /// <inheritdoc />
        public void ReserveRun (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (syncRoot)
            {
                if (runReservations.ContainsKey(cancellationToken))
                {
                    throw new InvalidOperationException(
                        "The Unix domain socket listener already has a reservation for the specified Run cancellation token.");
                }

                runReservations.Add(
                    cancellationToken,
                    new RunReservation(cancellationToken));
            }
        }

        private RunReservation ClaimRunReservation (CancellationToken cancellationToken)
        {
            lock (syncRoot)
            {
                if (!runReservations.TryGetValue(cancellationToken, out var runReservation))
                {
                    runReservation = new RunReservation(cancellationToken);
                    runReservations.Add(cancellationToken, runReservation);
                }

                if (!runReservation.TryClaim())
                {
                    throw new InvalidOperationException(
                        "The Unix domain socket listener Run reservation has already been claimed.");
                }

                return runReservation;
            }
        }

        private void RemoveRunReservation (RunReservation runReservation)
        {
            lock (syncRoot)
            {
                RemoveRunReservationWithoutLock(runReservation);
            }
        }

        private void RemoveRunReservationWithoutLock (RunReservation runReservation)
        {
            if (runReservations.TryGetValue(runReservation.CancellationToken, out var activeReservation)
                && ReferenceEquals(activeReservation, runReservation))
            {
                runReservations.Remove(runReservation.CancellationToken);
            }
        }

        private static async ValueTask<EndpointOwnershipLease> ClaimEndpointOwnershipAsync (
            string address,
            UnixSocketAccessBoundary accessBoundary,
            RunReservation runReservation,
            CancellationToken cancellationToken)
        {
            var normalizedAddress = Path.GetFullPath(address);
            var ownershipToken = new object();
            // The cross-process lock is retained for the active endpoint lifetime. This shorter gate serializes
            // process-local state discovery and registration without making a successor wait on that retained lock.
            await EndpointOwnershipClaimGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var processLocalOwnershipLease = ClaimProcessLocalEndpointOwnershipIfPresent(
                    normalizedAddress,
                    ownershipToken,
                    accessBoundary,
                    runReservation);
                if (processLocalOwnershipLease != null)
                {
                    return processLocalOwnershipLease;
                }

                var lockPath = ResolveEndpointOwnershipLockPath(normalizedAddress);
                FileExclusiveLock ownershipLock;
                try
                {
                    ownershipLock = await FileExclusiveLock.AcquireAsync(
                            lockPath,
                            EndpointOwnershipAcquireTimeout,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (TimeoutException exception)
                {
                    throw new TimeoutException(
                        $"Unix socket endpoint is already owned by another process. Address={normalizedAddress}",
                        exception);
                }

                try
                {
                    FileSystemAccessBoundary.EnsureSecureFile(lockPath);
                    lock (EndpointOwnershipSyncRoot)
                    {
                        var ownershipState = new EndpointOwnershipState(ownershipLock)
                        {
                            ActiveOwnershipToken = ownershipToken,
                            ActiveRunReservation = runReservation,
                            LeaseCount = 1,
                        };
                        ActiveEndpointOwners.Add(normalizedAddress, ownershipState);
                        ownershipLock = null;
                    }

                    return new EndpointOwnershipLease(
                        normalizedAddress,
                        ownershipToken,
                        accessBoundary);
                }
                finally
                {
                    ownershipLock?.Dispose();
                }
            }
            finally
            {
                EndpointOwnershipClaimGate.Release();
            }
        }

        private static EndpointOwnershipLease ClaimProcessLocalEndpointOwnershipIfPresent (
            string normalizedAddress,
            object ownershipToken,
            UnixSocketAccessBoundary accessBoundary,
            RunReservation runReservation)
        {
            lock (EndpointOwnershipSyncRoot)
            {
                if (!ActiveEndpointOwners.TryGetValue(normalizedAddress, out var ownershipState))
                {
                    return null;
                }

                // A lifecycle release can close a reserved Run after it claims process-local ownership but before bind.
                // The closed reservation authorizes its successor without waiting for the abandoned Run to unwind.
                if (ownershipState.ActiveOwnershipToken != null
                    && !ownershipState.AllowsSameProcessSuccessor
                    && !(ownershipState.ActiveRunReservation?.IsClosed ?? false))
                {
                    throw new InvalidOperationException(
                        $"Unix socket endpoint is already owned by an active listener in this process. Address={normalizedAddress}");
                }

                ownershipState.ActiveOwnershipToken = ownershipToken;
                ownershipState.ActiveRunReservation = runReservation;
                ownershipState.AllowsSameProcessSuccessor = false;
                ownershipState.LeaseCount++;
                return new EndpointOwnershipLease(
                    normalizedAddress,
                    ownershipToken,
                    accessBoundary);
            }
        }

        private static void ReleaseEndpointOwnership (
            string normalizedAddress,
            object ownershipToken,
            UnixSocketAccessBoundary accessBoundary)
        {
            FileExclusiveLock ownershipLockToDispose = null;
            try
            {
                lock (EndpointOwnershipSyncRoot)
                {
                    if (!ActiveEndpointOwners.TryGetValue(normalizedAddress, out var ownershipState))
                    {
                        return;
                    }

                    var ownsActiveEndpoint = ReferenceEquals(
                        ownershipState.ActiveOwnershipToken,
                        ownershipToken);
                    try
                    {
                        if (ownsActiveEndpoint)
                        {
                            accessBoundary.Cleanup();
                        }
                    }
                    finally
                    {
                        if (ownsActiveEndpoint)
                        {
                            ownershipState.ActiveOwnershipToken = null;
                            ownershipState.ActiveRunReservation = null;
                            ownershipState.AllowsSameProcessSuccessor = false;
                        }

                        ownershipState.LeaseCount--;
                        if (ownershipState.LeaseCount == 0)
                        {
                            ActiveEndpointOwners.Remove(normalizedAddress);
                            // Keep the stable lock file in place. Deleting it could let two successors
                            // lock different inodes while one process is opening the same path.
                            ownershipLockToDispose = ownershipState.OwnershipLock;
                        }
                    }
                }
            }
            finally
            {
                ownershipLockToDispose?.Dispose();
            }
        }

        internal static string ResolveEndpointOwnershipLockPath (string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Socket address must not be empty or whitespace.", nameof(address));
            }

            var normalizedAddress = Path.GetFullPath(address);
            var lockIdentityPath = UnixSocketPathUtilities.BuildFallbackSocketPath(
                EndpointOwnershipLockDirectoryPrefix,
                normalizedAddress);
            return Path.ChangeExtension(lockIdentityPath, ".lock");
        }

        private static void AllowSameProcessSuccessor (
            string normalizedAddress,
            object ownershipToken)
        {
            lock (EndpointOwnershipSyncRoot)
            {
                if (ActiveEndpointOwners.TryGetValue(normalizedAddress, out var ownershipState)
                    && ReferenceEquals(ownershipState.ActiveOwnershipToken, ownershipToken))
                {
                    ownershipState.AllowsSameProcessSuccessor = true;
                }
            }
        }

        private sealed class EndpointOwnershipState
        {
            public EndpointOwnershipState (FileExclusiveLock ownershipLock)
            {
                OwnershipLock = ownershipLock ?? throw new ArgumentNullException(nameof(ownershipLock));
            }

            public FileExclusiveLock OwnershipLock { get; }

            public object ActiveOwnershipToken { get; set; }

            public RunReservation ActiveRunReservation { get; set; }

            public int LeaseCount { get; set; }

            public bool AllowsSameProcessSuccessor { get; set; }
        }

        private sealed class EndpointOwnershipLease : IDisposable
        {
            private readonly string normalizedAddress;

            private readonly object ownershipToken;

            private readonly UnixSocketAccessBoundary accessBoundary;

            private int disposed;

            public EndpointOwnershipLease (
                string normalizedAddress,
                object ownershipToken,
                UnixSocketAccessBoundary accessBoundary)
            {
                this.normalizedAddress = normalizedAddress ?? throw new ArgumentNullException(nameof(normalizedAddress));
                this.ownershipToken = ownershipToken ?? throw new ArgumentNullException(nameof(ownershipToken));
                this.accessBoundary = accessBoundary ?? throw new ArgumentNullException(nameof(accessBoundary));
            }

            public void Dispose ()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                {
                    return;
                }

                ReleaseEndpointOwnership(
                    normalizedAddress,
                    ownershipToken,
                    accessBoundary);
            }

            public void AllowSameProcessSuccessor ()
            {
                if (Volatile.Read(ref disposed) == 0)
                {
                    UnixDomainSocketUnityIpcTransportListener.AllowSameProcessSuccessor(
                        normalizedAddress,
                        ownershipToken);
                }
            }
        }

        private sealed class RunReservation
        {
            private int isClaimed;

            private int isClosed;

            public RunReservation (CancellationToken cancellationToken)
            {
                CancellationToken = cancellationToken;
            }

            public CancellationToken CancellationToken { get; }

            public bool IsClosed => Volatile.Read(ref isClosed) != 0;

            public bool TryClaim ()
            {
                return Interlocked.CompareExchange(ref isClaimed, 1, 0) == 0;
            }

            public void Close ()
            {
                _ = Interlocked.Exchange(ref isClosed, 1);
            }
        }

        private void DisposeListenerSocket (Socket listenerSocket)
        {
            try
            {
                listenerSocket.Dispose();
            }
            catch (Exception exception) when (exception is ObjectDisposedException or SocketException or InvalidOperationException)
            {
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Transport,
                    "Unix socket listener cleanup failed unexpectedly.",
                    exception);
            }
        }

        private sealed class AcceptedUnixDomainSocketConnection : IDisposable
        {
            private readonly Socket socket;

            private readonly NetworkStream stream;

            private int disposed;

            public AcceptedUnixDomainSocketConnection (Socket socket)
            {
                this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
                stream = new NetworkStream(socket, ownsSocket: false);
            }

            public Stream Stream => stream;

            public void Dispose ()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                {
                    return;
                }

                try
                {
                    stream.Dispose();
                }
                catch (Exception exception) when (exception is ObjectDisposedException or IOException or SocketException or InvalidOperationException)
                {
                }

                try
                {
                    socket.Dispose();
                }
                catch (Exception exception) when (exception is ObjectDisposedException or SocketException or InvalidOperationException)
                {
                }
            }
        }
    }
}
