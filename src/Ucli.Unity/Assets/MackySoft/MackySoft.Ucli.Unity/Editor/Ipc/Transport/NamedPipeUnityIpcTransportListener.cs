using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements named-pipe transport accept loop for Unity IPC server. </summary>
    internal sealed class NamedPipeUnityIpcTransportListener :
        IUnityIpcTransportListener,
        IUnityIpcTransportRunReservation
    {
        private const string EndpointOwnershipLockDirectoryName = "ipc-listener-locks";

        private static readonly TimeSpan EndpointOwnershipAcquireTimeout = TimeSpan.FromSeconds(1);

        private readonly object syncRoot = new object();

        private readonly Dictionary<CancellationToken, RunReservation> runReservations =
            new Dictionary<CancellationToken, RunReservation>();

        private readonly IDaemonLogger daemonLogger;

        private readonly int maximumActiveConnections;

        private readonly TimeSpan connectionDrainTimeout;

        private ListenerGeneration activeListenerGeneration;

        /// <summary> Initializes a new instance of the <see cref="NamedPipeUnityIpcTransportListener" /> class. </summary>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        /// <param name="maximumActiveConnections"> The maximum number of accepted connections that may be handled concurrently. </param>
        /// <param name="connectionDrainTimeout"> The maximum time allowed for active connections to finish during listener shutdown. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonLogger" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when a numeric limit is not positive. </exception>
        public NamedPipeUnityIpcTransportListener (
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
        public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

        /// <summary> Runs transport-specific accept loop until cancellation is requested. </summary>
        /// <param name="endpointBinding"> The guarded runtime endpoint binding for this listener generation. </param>
        /// <param name="connectionHandler"> The connection handler dependency. </param>
        /// <param name="cancellationToken"> The cancellation token for listener lifecycle. </param>
        /// <exception cref="ArgumentNullException"> Thrown when a required dependency is <see langword="null" />. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when the endpoint binding does not represent a named pipe. </exception>
        public async Task RunAsync (
            UnityIpcEndpointBinding endpointBinding,
            IUnityIpcConnectionHandler connectionHandler,
            Action onStarted,
            Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
            CancellationToken cancellationToken)
        {
            if (endpointBinding == null)
            {
                throw new ArgumentNullException(nameof(endpointBinding));
            }

            if (endpointBinding.Endpoint.TransportKind != IpcTransportKind.NamedPipe)
            {
                throw new InvalidOperationException(
                    $"Named pipe listener cannot bind transport kind '{endpointBinding.Endpoint.TransportKind}'.");
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

            var runReservation = ClaimRunReservation(cancellationToken);
            if (runReservation.IsClosed)
            {
                RemoveRunReservation(runReservation);
                return;
            }

            var started = false;
            var connectionGroup = new UnityIpcTransportConnectionGroup(
                daemonLogger,
                maximumActiveConnections);

            try
            {
                ListenerGeneration listenerGeneration = null;
                CancellationTokenRegistration cancellationRegistration = default;
                try
                {
                    var ownershipLease = await AcquireEndpointOwnershipAsync(
                        endpointBinding,
                        cancellationToken);
                    listenerGeneration = new ListenerGeneration(
                        ownershipLease,
                        connectionGroup);
                    if (!TryActivateListenerGeneration(listenerGeneration, runReservation))
                    {
                        return;
                    }

                    cancellationRegistration = cancellationToken.Register(
                        () => _ = CloseListenerGeneration(listenerGeneration));

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Creating the next server instance establishes listener availability. A failure here is
                        // generation-fatal; retrying in this loop would keep StartAsync pending or leave a published
                        // generation unreachable while spinning indefinitely.
                        var serverStream = listenerGeneration.TryCreateServerStream(
                            () => PipeServerStreamFactory.Create(endpointBinding, daemonLogger));
                        if (serverStream == null)
                        {
                            return;
                        }

                        var serverStreamDetached = false;

                        try
                        {
                            if (!started)
                            {
                                if (!listenerGeneration.TryInvokeForActiveServerStream(serverStream, onStarted))
                                {
                                    return;
                                }

                                started = true;
                            }

                            // Closing the generation's server stream is the single cancellation mechanism.
                            // Unity's Windows Mono runtime can otherwise dispose the pipe operation's internal
                            // cancellation source before its overlapped I/O callback completes during Domain Reload.
                            await serverStream.WaitForConnectionAsync()
                                .ConfigureAwait(false);
                            if (!listenerGeneration.TryDetachConnectedStream(serverStream))
                            {
                                return;
                            }

                            serverStreamDetached = true;
                            var connectedStream = serverStream;
                            _ = connectionGroup.TryStart(
                                connectedStream,
                                () => connectionHandler.HandleAsync(connectedStream, cancellationToken),
                                onConnectionCompleted,
                                cancellationToken);
                            serverStream = null;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (IOException exception) when (
                            cancellationToken.IsCancellationRequested
                            || listenerGeneration.IsClosed)
                        {
                            daemonLogger.Info(
                                DaemonLogCategories.Transport,
                                $"Named pipe listener stopped: {exception.Message}");
                            return;
                        }
                        catch (ObjectDisposedException exception) when (
                            cancellationToken.IsCancellationRequested
                            || listenerGeneration.IsClosed)
                        {
                            daemonLogger.Info(
                                DaemonLogCategories.Transport,
                                $"Named pipe listener disposed during shutdown: {exception.Message}");
                            return;
                        }
                        catch (Exception exception) when (
                            !cancellationToken.IsCancellationRequested
                            && !listenerGeneration.IsClosed
                            && (exception is IOException or InvalidDataException))
                        {
                            // NOTE: Probe callers may close timed-out streams while Unity is busy on the main thread.
                            // Emitting these expected connection-local failures to the Unity console can make recovery slower.
                        }
                        finally
                        {
                            if (serverStream != null)
                            {
                                if (serverStreamDetached)
                                {
                                    await Task.Run(serverStream.Dispose)
                                        .ConfigureAwait(false);
                                }
                                else
                                {
                                    await listenerGeneration.ReleaseServerStreamAsync(serverStream)
                                        .ConfigureAwait(false);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    cancellationRegistration.Dispose();
                    RemoveRunReservation(runReservation);
                    if (listenerGeneration != null)
                    {
                        await CompleteListenerGenerationAsync(listenerGeneration)
                            .ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                connectionGroup.Release();
                await connectionGroup.WaitForCompletionAsync(connectionDrainTimeout)
                    .ConfigureAwait(false);
            }
        }

        /// <summary> Marks the active listener generation released and initiates non-blocking transport cleanup. </summary>
        public void Release ()
        {
            ListenerGeneration listenerGeneration;
            lock (syncRoot)
            {
                foreach (var runReservation in runReservations.Values)
                {
                    runReservation.Close();
                }

                listenerGeneration = activeListenerGeneration;
                activeListenerGeneration = null;
            }

            if (listenerGeneration != null)
            {
                _ = listenerGeneration.Close();
            }
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
                        "The named pipe listener already has a reservation for the specified Run cancellation token.");
                }

                runReservations.Add(
                    cancellationToken,
                    new RunReservation(cancellationToken));
            }
        }

        private bool TryActivateListenerGeneration (
            ListenerGeneration listenerGeneration,
            RunReservation runReservation)
        {
            lock (syncRoot)
            {
                RemoveRunReservationWithoutLock(runReservation);
                if (runReservation.IsClosed)
                {
                    return false;
                }

                if (activeListenerGeneration != null)
                {
                    throw new InvalidOperationException(
                        "The named pipe listener already has an active RunAsync generation.");
                }

                activeListenerGeneration = listenerGeneration;
                return true;
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
                        "The named pipe listener Run reservation has already been claimed.");
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

        private Task CloseListenerGeneration (ListenerGeneration listenerGeneration)
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(activeListenerGeneration, listenerGeneration))
                {
                    activeListenerGeneration = null;
                }
            }

            return listenerGeneration.Close();
        }

        private Task CompleteListenerGenerationAsync (ListenerGeneration listenerGeneration)
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(activeListenerGeneration, listenerGeneration))
                {
                    activeListenerGeneration = null;
                }
            }

            return listenerGeneration.CompleteRunAsync();
        }

        private static async ValueTask<FileExclusiveLock> AcquireEndpointOwnershipAsync (
            UnityIpcEndpointBinding endpointBinding,
            CancellationToken cancellationToken)
        {
            var lockPath = ResolveEndpointOwnershipLockPath(endpointBinding);
            try
            {
                return await FileExclusiveLock.AcquireAsync(
                        lockPath,
                        EndpointOwnershipAcquireTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                throw new TimeoutException(
                    "Named pipe endpoint is already owned by another listener generation. " +
                    $"Address={endpointBinding.Endpoint.Address}",
                    exception);
            }
        }

        internal static AbsolutePath ResolveEndpointOwnershipLockPath (
            UnityIpcEndpointBinding endpointBinding)
        {
            if (endpointBinding == null)
            {
                throw new ArgumentNullException(nameof(endpointBinding));
            }

            if (endpointBinding.Endpoint.TransportKind != IpcTransportKind.NamedPipe)
            {
                throw new InvalidOperationException(
                    $"Named pipe ownership cannot resolve transport kind '{endpointBinding.Endpoint.TransportKind}'.");
            }

            var localApplicationDataPath = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            if (!AbsolutePath.TryParse(
                    localApplicationDataPath,
                    out var localApplicationDataRoot,
                    out var localApplicationDataPathFailure))
            {
                throw new InvalidOperationException(
                    "Current-user local application data path could not be resolved for named pipe endpoint ownership. " +
                    localApplicationDataPathFailure.Message);
            }

            var pipeName = endpointBinding.Endpoint.Address;
            var normalizedAddress = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? pipeName.ToUpperInvariant()
                : pipeName;
            var addressHash = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(normalizedAddress));
            var lockRelativePath = RootRelativePath.Parse(
                $"ucli/{EndpointOwnershipLockDirectoryName}/{addressHash}.lock");
            return ContainedPath.Create(localApplicationDataRoot, lockRelativePath).Target;
        }

        private sealed class ListenerGeneration
        {
            private readonly object syncRoot = new object();

            private FileExclusiveLock ownershipLease;

            private readonly UnityIpcTransportConnectionGroup connectionGroup;

            private NamedPipeServerStream activeServerStream;

            private Task serverStreamCleanupTask = Task.CompletedTask;

            private Task closeTask;

            private Task runCompletionTask;

            private bool isClosed;

            public ListenerGeneration (
                FileExclusiveLock ownershipLease,
                UnityIpcTransportConnectionGroup connectionGroup)
            {
                this.ownershipLease = ownershipLease
                    ?? throw new ArgumentNullException(nameof(ownershipLease));
                this.connectionGroup = connectionGroup
                    ?? throw new ArgumentNullException(nameof(connectionGroup));
            }

            public bool IsClosed
            {
                get
                {
                    lock (syncRoot)
                    {
                        return isClosed;
                    }
                }
            }

            public NamedPipeServerStream TryCreateServerStream (
                Func<NamedPipeServerStream> createServerStream)
            {
                if (createServerStream == null)
                {
                    throw new ArgumentNullException(nameof(createServerStream));
                }

                lock (syncRoot)
                {
                    if (isClosed)
                    {
                        return null;
                    }

                    if (activeServerStream != null)
                    {
                        throw new InvalidOperationException(
                            "The named pipe listener generation already owns an active server stream.");
                    }

                    activeServerStream = createServerStream();
                    return activeServerStream;
                }
            }

            public bool TryInvokeForActiveServerStream (
                NamedPipeServerStream serverStream,
                Action action)
            {
                if (serverStream == null)
                {
                    throw new ArgumentNullException(nameof(serverStream));
                }

                if (action == null)
                {
                    throw new ArgumentNullException(nameof(action));
                }

                lock (syncRoot)
                {
                    if (isClosed || !ReferenceEquals(activeServerStream, serverStream))
                    {
                        return false;
                    }

                    action();
                    return !isClosed && ReferenceEquals(activeServerStream, serverStream);
                }
            }

            public bool TryDetachConnectedStream (NamedPipeServerStream serverStream)
            {
                if (serverStream == null)
                {
                    throw new ArgumentNullException(nameof(serverStream));
                }

                lock (syncRoot)
                {
                    if (isClosed || !ReferenceEquals(activeServerStream, serverStream))
                    {
                        return false;
                    }

                    activeServerStream = null;
                    return true;
                }
            }

            public Task ReleaseServerStreamAsync (NamedPipeServerStream serverStream)
            {
                if (serverStream == null)
                {
                    throw new ArgumentNullException(nameof(serverStream));
                }

                lock (syncRoot)
                {
                    if (ReferenceEquals(activeServerStream, serverStream))
                    {
                        activeServerStream = null;
                        var streamCleanupTask = Task.Run(serverStream.Dispose);
                        serverStreamCleanupTask = Task.WhenAll(
                            serverStreamCleanupTask,
                            streamCleanupTask);
                        return streamCleanupTask;
                    }

                    return closeTask ?? Task.CompletedTask;
                }
            }

            public Task Close ()
            {
                lock (syncRoot)
                {
                    if (closeTask != null)
                    {
                        return closeTask;
                    }

                    isClosed = true;
                    var serverStream = activeServerStream;
                    activeServerStream = null;
                    if (serverStream != null)
                    {
                        serverStreamCleanupTask = Task.WhenAll(
                            serverStreamCleanupTask,
                            Task.Run(serverStream.Dispose));
                    }

                    connectionGroup.Release();
                    closeTask = serverStreamCleanupTask;
                    return closeTask;
                }
            }

            public Task CompleteRunAsync ()
            {
                lock (syncRoot)
                {
                    if (runCompletionTask != null)
                    {
                        return runCompletionTask;
                    }

                    var listenerCloseTask = Close();
                    var lease = ownershipLease;
                    ownershipLease = null;
                    runCompletionTask = CompleteRunCoreAsync(
                        listenerCloseTask,
                        lease);
                    return runCompletionTask;
                }
            }

            private static async Task CompleteRunCoreAsync (
                Task listenerCloseTask,
                FileExclusiveLock ownershipLease)
            {
                try
                {
                    await listenerCloseTask.ConfigureAwait(false);
                }
                finally
                {
                    await Task.Run(ownershipLease.Dispose)
                        .ConfigureAwait(false);
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

        private static class PipeServerStreamFactory
        {
            public static NamedPipeServerStream Create (
                UnityIpcEndpointBinding endpointBinding,
                IDaemonLogger daemonLogger)
            {
                var pipeName = endpointBinding.Endpoint.Address;
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                }

                if (!TryCreateCurrentUserOnlySecurity(out var pipeSecurity, out var failureReason))
                {
                    daemonLogger.Error(
                        DaemonLogCategories.Transport,
                        "Named pipe listener could not resolve the current Windows user SID. Refusing to start without an explicit current-user ACL.",
                        failureReason);
                    throw new InvalidOperationException(
                        $"Named pipe listener could not resolve the current Windows user SID. Refusing to start without an explicit current-user ACL. {failureReason}");
                }

                return new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    0,
                    0,
                    pipeSecurity);
            }

            private static bool TryCreateCurrentUserOnlySecurity (
                out PipeSecurity pipeSecurity,
                out string failureReason)
            {
                pipeSecurity = null;
                if (!TryResolveCurrentUserSid(out var currentUserSid, out failureReason))
                {
                    return false;
                }

                pipeSecurity = new PipeSecurity();
                pipeSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    currentUserSid,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));
                failureReason = string.Empty;
                return true;
            }

            private static bool TryResolveCurrentUserSid (
                out SecurityIdentifier securityIdentifier,
                out string failureReason)
            {
                securityIdentifier = null;
                var errors = new List<string>(3);

                try
                {
                    using var identity = WindowsIdentity.GetCurrent();
                    if (TryGetIdentitySecurityIdentifier(identity, static currentIdentity => currentIdentity.User, "WindowsIdentity.User", out securityIdentifier, out var error))
                    {
                        failureReason = string.Empty;
                        return true;
                    }

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        errors.Add(error);
                    }

                    if (TryGetIdentitySecurityIdentifier(identity, static currentIdentity => currentIdentity.Owner, "WindowsIdentity.Owner", out securityIdentifier, out error))
                    {
                        failureReason = string.Empty;
                        return true;
                    }

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        errors.Add(error);
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or NotImplementedException or PlatformNotSupportedException)
                {
                    errors.Add($"WindowsIdentity.GetCurrent failed: {exception.GetType().Name}: {exception.Message}");
                }

                if (TryTranslateCurrentUserAccount(out securityIdentifier, out var accountError))
                {
                    failureReason = string.Empty;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(accountError))
                {
                    errors.Add(accountError);
                }

                if (TryResolveCurrentUserSidFromAccessToken(out securityIdentifier, out var accessTokenError))
                {
                    failureReason = string.Empty;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(accessTokenError))
                {
                    errors.Add(accessTokenError);
                }

                failureReason = errors.Count == 0
                    ? "Current Windows user SID could not be resolved."
                    : string.Join(" | ", errors);
                return false;
            }

            private static bool TryGetIdentitySecurityIdentifier (
                WindowsIdentity identity,
                Func<WindowsIdentity, IdentityReference> identityReferenceSelector,
                string sourceName,
                out SecurityIdentifier securityIdentifier,
                out string failureReason)
            {
                securityIdentifier = null;
                try
                {
                    var identityReference = identityReferenceSelector(identity);
                    if (identityReference == null)
                    {
                        failureReason = $"{sourceName} returned null.";
                        return false;
                    }

                    if (identityReference is SecurityIdentifier typedSecurityIdentifier)
                    {
                        securityIdentifier = typedSecurityIdentifier;
                        failureReason = string.Empty;
                        return true;
                    }

                    securityIdentifier = (SecurityIdentifier)identityReference.Translate(typeof(SecurityIdentifier));
                    failureReason = string.Empty;
                    return true;
                }
                catch (Exception exception) when (exception is IdentityNotMappedException or InvalidOperationException or InvalidCastException or NotImplementedException or PlatformNotSupportedException)
                {
                    failureReason = $"{sourceName} failed: {exception.GetType().Name}: {exception.Message}";
                    return false;
                }
            }

            private static bool TryTranslateCurrentUserAccount (
                out SecurityIdentifier securityIdentifier,
                out string failureReason)
            {
                securityIdentifier = null;

                var userName = Environment.UserName;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    failureReason = "Environment.UserName returned an empty user name.";
                    return false;
                }

                var domainName = Environment.UserDomainName;
                var accountName = string.IsNullOrWhiteSpace(domainName)
                    ? userName
                    : string.Concat(domainName, "\\", userName);

                try
                {
                    securityIdentifier = (SecurityIdentifier)new NTAccount(accountName).Translate(typeof(SecurityIdentifier));
                    failureReason = string.Empty;
                    return true;
                }
                catch (Exception exception) when (exception is IdentityNotMappedException or InvalidCastException or InvalidOperationException or NotImplementedException or PlatformNotSupportedException)
                {
                    failureReason = $"NTAccount translation failed for '{accountName}': {exception.GetType().Name}: {exception.Message}";
                    return false;
                }
            }

            private static bool TryResolveCurrentUserSidFromAccessToken (
                out SecurityIdentifier securityIdentifier,
                out string failureReason)
            {
                securityIdentifier = null;

                IntPtr accessTokenHandle = IntPtr.Zero;
                IntPtr tokenUserBuffer = IntPtr.Zero;

                try
                {
                    if (!OpenProcessToken(GetCurrentProcess(), TokenQueryAccess, out accessTokenHandle))
                    {
                        failureReason = CreateWin32FailureReason("OpenProcessToken");
                        return false;
                    }

                    _ = GetTokenInformation(
                        accessTokenHandle,
                        TokenInformationClass.TokenUser,
                        IntPtr.Zero,
                        0,
                        out var requiredBufferLength);

                    var probeErrorCode = Marshal.GetLastWin32Error();
                    if ((requiredBufferLength <= 0) && (probeErrorCode != ErrorInsufficientBuffer))
                    {
                        failureReason = CreateWin32FailureReason("GetTokenInformation probe", probeErrorCode);
                        return false;
                    }

                    tokenUserBuffer = Marshal.AllocHGlobal(requiredBufferLength);
                    if (!GetTokenInformation(
                            accessTokenHandle,
                            TokenInformationClass.TokenUser,
                            tokenUserBuffer,
                            requiredBufferLength,
                            out _))
                    {
                        failureReason = CreateWin32FailureReason("GetTokenInformation");
                        return false;
                    }

                    var tokenUser = Marshal.PtrToStructure<TokenUser>(tokenUserBuffer);
                    if (tokenUser.UserSid == IntPtr.Zero)
                    {
                        failureReason = "GetTokenInformation(TokenUser) returned a null SID pointer.";
                        return false;
                    }

                    securityIdentifier = new SecurityIdentifier(tokenUser.UserSid);
                    failureReason = string.Empty;
                    return true;
                }
                catch (Exception exception) when (exception is InvalidOperationException or InvalidCastException or PlatformNotSupportedException)
                {
                    failureReason = $"Access token SID resolution failed: {exception.GetType().Name}: {exception.Message}";
                    return false;
                }
                finally
                {
                    if (tokenUserBuffer != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(tokenUserBuffer);
                    }

                    if (accessTokenHandle != IntPtr.Zero)
                    {
                        CloseHandle(accessTokenHandle);
                    }
                }
            }

            private static string CreateWin32FailureReason (
                string operationName,
                int? errorCode = null)
            {
                var effectiveErrorCode = errorCode ?? Marshal.GetLastWin32Error();
                return $"{operationName} failed with Win32 error {effectiveErrorCode}: {new Win32Exception(effectiveErrorCode).Message}";
            }

            [DllImport("advapi32.dll", SetLastError = true)]
            private static extern bool GetTokenInformation (
                IntPtr tokenHandle,
                TokenInformationClass tokenInformationClass,
                IntPtr tokenInformation,
                int tokenInformationLength,
                out int returnLength);

            [DllImport("advapi32.dll", SetLastError = true)]
            private static extern bool OpenProcessToken (
                IntPtr processHandle,
                uint desiredAccess,
                out IntPtr tokenHandle);

            [DllImport("kernel32.dll")]
            private static extern IntPtr GetCurrentProcess ();

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool CloseHandle (IntPtr handle);

            private const uint TokenQueryAccess = 0x0008;
            private const int ErrorInsufficientBuffer = 122;

            private enum TokenInformationClass
            {
                TokenUser = 1,
            }

            [StructLayout(LayoutKind.Sequential)]
            private readonly struct TokenUser
            {
                public readonly IntPtr UserSid;
                public readonly uint Attributes;
            }
        }
    }
}
