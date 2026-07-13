using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;
using MackySoft.Ucli.Unity.Runtime;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Bootstraps IPC daemon server registration for non-batchmode Unity GUI Editor sessions. </summary>
    internal static class UnityGuiBootstrap
    {
        private static readonly object SyncRoot = new object();

        private static readonly SemaphoreSlim LifecycleGate = new SemaphoreSlim(1, 1);

        private static readonly TimeSpan LifecycleSidecarReloadFlushTimeout = TimeSpan.FromMilliseconds(500);

        private static readonly TimeSpan LifecycleSidecarWriterStopTimeout = TimeSpan.FromMilliseconds(500);

        private static readonly TimeSpan LifecycleSidecarAsyncCleanupTimeout = TimeSpan.FromSeconds(2);

        private static ActiveGuiBootstrapState activeState;

        private static StartingGuiBootstrapState startingState;

        private static ActiveGuiBootstrapState stoppingState;

        private static FailedStartResourceRetention failedStartResourceRetention;

        private static IUnityMutationExecutionState stoppedMutationFence;

        private static bool isBootstrapRestartBlocked;

        /// <summary> Starts or replaces the active GUI daemon session registration. </summary>
        /// <param name="bootstrapArguments"> Optional CLI GUI bootstrap arguments. </param>
        /// <param name="sessionReplacementScope"> The scope of existing current-process GUI sessions that may be replaced. </param>
        /// <param name="cancellationToken"> The cancellation token for bootstrap admission and startup. </param>
        /// <returns> A task that produces the GUI endpoint registration result. </returns>
        public static Task<UnityGuiBootstrapStartResult> StartAsync (
            IpcGuiBootstrapArguments bootstrapArguments,
            UnityGuiSessionReplacementScope sessionReplacementScope,
            CancellationToken cancellationToken)
        {
            ValidateSessionReplacementScope(sessionReplacementScope);
            var editorInstanceId = UnityEditorSessionStateStore.GetOrCreateEditorInstanceId();
            var daemonLogStream = new DaemonLogRingBuffer();
            var daemonLogger = new DaemonLogger(
                daemonLogStream,
                UnityMainThreadDaemonConsoleLogSink.CaptureCurrent());
            var nextStartingState = new StartingGuiBootstrapState(
                cancellationToken,
                editorInstanceId,
                daemonLogger);
            lock (SyncRoot)
            {
                if (startingState != null)
                {
                    nextStartingState.DisposeCancellationSource();
                    return Task.FromResult(UnityGuiBootstrapStartResult.Failure(
                        "A GUI daemon startup generation is already in progress."));
                }

                startingState = nextStartingState;
            }

            EnsureEditorLifecycleSubscriptions();
            return StartTrackedAsync(
                bootstrapArguments,
                sessionReplacementScope,
                daemonLogStream,
                nextStartingState);
        }

        private static async Task<UnityGuiBootstrapStartResult> StartTrackedAsync (
            IpcGuiBootstrapArguments bootstrapArguments,
            UnityGuiSessionReplacementScope sessionReplacementScope,
            DaemonLogRingBuffer daemonLogStream,
            StartingGuiBootstrapState state)
        {
            var lifecycleGateEntered = false;
            try
            {
                await LifecycleGate.WaitAsync(state.CancellationToken);
                lifecycleGateEntered = true;
                EnsureStartingGenerationOwnership(state);
                ActiveGuiBootstrapState capturedState;
                ActiveGuiBootstrapState capturedStoppingState;
                IUnityMutationExecutionState capturedMutationFence;
                bool capturedBootstrapRestartBlocked;
                lock (SyncRoot)
                {
                    capturedState = activeState;
                    capturedStoppingState = stoppingState;
                    capturedMutationFence = stoppedMutationFence;
                    capturedBootstrapRestartBlocked = isBootstrapRestartBlocked;
                    if (capturedMutationFence != null && !capturedMutationFence.IsBusy)
                    {
                        stoppedMutationFence = null;
                        capturedMutationFence = null;
                    }
                }

                if (capturedBootstrapRestartBlocked)
                {
                    return UnityGuiBootstrapStartResult.Failure(
                        "A prior GUI daemon startup could not release its listener safely. Restart the Unity Editor before starting a new daemon session.");
                }

                if (capturedStoppingState != null)
                {
                    return UnityGuiBootstrapStartResult.Failure(
                        "The previous GUI daemon did not terminate safely. Restart the Unity Editor before starting a new daemon session.");
                }

                if (capturedMutationFence != null)
                {
                    return UnityGuiBootstrapStartResult.Failure(
                        "The previous GUI daemon still has an unfinished Unity mutation. Restart the Unity Editor before starting a new daemon session.");
                }

                if (capturedState != null)
                {
                    if (!capturedState.ShutdownSignal.IsSignaled)
                    {
                        if (!CanReplaceActiveSession(
                                sessionReplacementScope,
                                capturedState.MutationLaneControl.IsBusy))
                        {
                            if (sessionReplacementScope == UnityGuiSessionReplacementScope.AnyCurrentProcessSession
                                && capturedState.MutationLaneControl.IsBusy)
                            {
                                return UnityGuiBootstrapStartResult.Failure(
                                    "The active GUI daemon has an unfinished Unity mutation. Restart the Unity Editor before rebootstrap.");
                            }

                            return UnityGuiBootstrapStartResult.AlreadyRunning();
                        }
                    }

                    if (!capturedState.MutationLaneControl.TrySealAdmissionWhenIdle(out var mutationAdmissionSeal))
                    {
                        var failureMessage = capturedState.MutationLaneControl.IsPoisoned
                            ? "The active GUI daemon has indeterminate Unity mutation state. Restart the Unity Editor before rebootstrap."
                            : "The active GUI daemon admitted a Unity mutation before replacement could begin. Retry after it completes or restart the Unity Editor.";
                        return UnityGuiBootstrapStartResult.Failure(failureMessage);
                    }

                    // NOTE:
                    // daemon stop writes the shutdown response before the monitor clears activeState.
                    // A supervisor rebootstrap can arrive in that small window, so StartAsync owns the
                    // pending stop before creating a replacement daemon endpoint. Keep mutation admission
                    // sealed until the old service provider has been disposed.
                    using (mutationAdmissionSeal)
                    {
                        var stoppedSafely = await StopStateAsync(
                            capturedState,
                            requestProcessExit: false,
                            deleteSession: true,
                            trackStoppedMutationFence: false);
                        if (!stoppedSafely)
                        {
                            return UnityGuiBootstrapStartResult.Failure(
                                "The previous GUI daemon did not terminate before its stop deadline. Restart the Unity Editor before rebootstrap.");
                        }
                    }
                }

                EnsureStartingGenerationOwnership(state);
                return await StartUnlockedAsync(
                    bootstrapArguments: bootstrapArguments,
                    sessionReplacementScope: sessionReplacementScope,
                    daemonLogStream: daemonLogStream,
                    state: state);
            }
            catch (OperationCanceledException) when (
                state.CancellationToken.IsCancellationRequested
                && !state.CallerCancellationToken.IsCancellationRequested)
            {
                return UnityGuiBootstrapStartResult.Failure(
                    "GUI daemon startup was canceled by the Unity Editor lifecycle.");
            }
            finally
            {
                if (lifecycleGateEntered)
                {
                    LifecycleGate.Release();
                }

                CompleteUnusedStartingGeneration(state);
            }
        }

        private static async Task<UnityGuiBootstrapStartResult> StartUnlockedAsync (
            IpcGuiBootstrapArguments bootstrapArguments,
            UnityGuiSessionReplacementScope sessionReplacementScope,
            DaemonLogRingBuffer daemonLogStream,
            StartingGuiBootstrapState state)
        {
            var daemonLogger = state.DaemonLogger;
            ActiveGuiBootstrapState nextState = null;
            UnityGuiSessionPersistence.PreparedSession preparedSession = null;
            UnityGuiSessionRegistration registration = null;
            IUnityIpcServer server = null;
            UnityLogCaptureService unityLogCaptureService = null;
            IServiceProvider serviceProvider = null;
            try
            {
                var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
                var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectRoot);
                var projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectRoot);
                var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, projectFingerprint);
                var sessionOptions = UnityGuiBootstrapSessionOptions.Create(bootstrapArguments);
                preparedSession = await UnityGuiSessionPersistence.PrepareAsync(
                    storageRoot,
                    projectFingerprint,
                    endpoint,
                    sessionOptions,
                    state.EditorInstanceId,
                    sessionReplacementScope: sessionReplacementScope,
                    cancellationToken: state.CancellationToken);
                if (!TryAttachPreparedSession(state, preparedSession))
                {
                    preparedSession.Dispose();
                    preparedSession = null;
                    throw CreateStartingGenerationCancellation(state);
                }

                registration = preparedSession.Registration;

                var daemonBootstrapArguments = new IpcDaemonBootstrapArguments(
                    RepositoryRoot: storageRoot,
                    ProjectFingerprint: projectFingerprint,
                    SessionPath: preparedSession.SessionPath,
                    SessionIssuedAtUtc: preparedSession.Registration.IssuedAtUtc,
                    EndpointTransportKind: ContractLiteralCodec.ToValue(endpoint.TransportKind),
                    EndpointAddress: endpoint.Address);
                var services = new ServiceCollection();
                services
                    .AddUnityIpcApplicationServices(
                        new ExactSessionTokenValidator(preparedSession.Registration.SessionToken),
                        projectFingerprint,
                        daemonLogger,
                        DaemonEditorMode.Gui)
                    .AddUnityIpcDaemonHostServices(
                        daemonBootstrapArguments,
                        daemonLogStream,
                        state.EditorInstanceId);

                serviceProvider = services.BuildServiceProvider();
                server = serviceProvider.GetRequiredService<IUnityIpcServer>();
                var readinessGate = serviceProvider.GetRequiredService<IUnityEditorReadinessGate>();
                var mutationLaneControl = serviceProvider.GetRequiredService<IUnityMutationLaneControl>();
                var shutdownSignal = serviceProvider.GetRequiredService<IDaemonShutdownSignal>();
                var serverVersion = serviceProvider.GetRequiredService<IServerVersionProvider>().GetVersion();
                unityLogCaptureService = serviceProvider.GetRequiredService<UnityLogCaptureService>();
                if (!TryAttachStartingResources(
                        state,
                        server,
                        unityLogCaptureService,
                        serviceProvider))
                {
                    ReleaseResourcesForEditorLifecycleEvent(
                        null,
                        server,
                        unityLogCaptureService,
                        serviceProvider,
                        daemonLogger,
                        deleteSession: false);
                    server = null;
                    unityLogCaptureService = null;
                    serviceProvider = null;
                    throw CreateStartingGenerationCancellation(state);
                }

                StartUnityLogCaptureForStartingGeneration(state, unityLogCaptureService);

                var startResult = await StartServerAndPublishSessionAsync(
                    server,
                    endpoint,
                    state.CancellationToken,
                    () => EnsureStartingGenerationOwnership(state),
                    () => BeginTrackedSessionPublication(state, preparedSession));
                registration = startResult.Registration;
                using var publicationFence = startResult.PublicationFence;
                preparedSession = null;
                EnsureStartingGenerationOwnership(state);
                var initialSnapshot = readinessGate.CaptureSnapshot();
                var initialSnapshotObservedAtUtc = initialSnapshot.ObservedAtUtc ?? DateTimeOffset.UtcNow;
                initialSnapshot = initialSnapshot with
                {
                    ObservedAtUtc = initialSnapshotObservedAtUtc,
                };
                var lifecycleSidecarWriter = new UnityLifecycleSidecarWriter(
                    new UnityLifecycleSidecarPersistence(
                        storageRoot,
                        projectFingerprint,
                        state.EditorInstanceId,
                        serverVersion));
                if (!TryAttachLifecycleSidecarWriter(state, lifecycleSidecarWriter))
                {
                    await lifecycleSidecarWriter.StopAsync(CancellationToken.None);
                    throw CreateStartingGenerationCancellation(state);
                }

                await lifecycleSidecarWriter.InitializeAsync(
                    initialSnapshot,
                    initialSnapshotObservedAtUtc,
                    state.CancellationToken);
                EnsureStartingGenerationOwnership(state);
                nextState = new ActiveGuiBootstrapState(
                    registration,
                    server,
                    shutdownSignal,
                    unityLogCaptureService,
                    serviceProvider,
                    daemonLogger,
                    readinessGate,
                    mutationLaneControl,
                    lifecycleSidecarWriter);
                state.EnsurePreparedSessionPublicationReadyForCommit();
                var startedResult = UnityGuiBootstrapStartResult.Started();
                state.DisposeCancellationSource();
                if (!publicationFence.TryCommitActiveOwnership(
                        () => TransferStartingGenerationToActive(state, nextState)))
                {
                    throw new InvalidOperationException(
                        "GUI IPC listener terminated before its session publication could become active.");
                }

                state.ReleasePreparedSessionAfterSuccessfulPublication();
                LogInfoBestEffort(
                    daemonLogger,
                    $"uCLI GUI daemon registered. storageRoot={storageRoot}, fingerprint={projectFingerprint}, endpoint={endpoint.Address}");
                _ = MonitorAsync(nextState);
                return startedResult;
            }
            catch (Exception exception)
            {
                var isCallerCancellation = exception is OperationCanceledException
                    && state.CallerCancellationToken.IsCancellationRequested;
                preparedSession = null;
                var isLifecycleCancellation = exception is OperationCanceledException
                    && state.CancellationToken.IsCancellationRequested
                    && !isCallerCancellation;
                if (!isCallerCancellation && !isLifecycleCancellation)
                {
                    daemonLogger.Exception(
                        DaemonLogCategories.Lifecycle,
                        "uCLI GUI daemon bootstrap failed.",
                        exception);
                }

                if (IsActiveGeneration(nextState))
                {
                    await StopStateAsync(
                        nextState,
                        requestProcessExit: false,
                        deleteSession: false,
                        trackStoppedMutationFence: true);
                    state.SchedulePreparedSessionCleanupAfterPublicationTerminates(
                        "after active startup failure server stop");
                    await state.PreparedSessionFinalization;
                    state.LogPreparedSessionFinalizationWarning();
                    if (isCallerCancellation)
                    {
                        throw;
                    }

                    return UnityGuiBootstrapStartResult.Failure(exception.Message);
                }

                await CleanupTrackedFailedStartAsync(state);

                if (isCallerCancellation)
                {
                    throw;
                }

                return UnityGuiBootstrapStartResult.Failure(exception.Message);
            }
        }

        private static void ValidateSessionReplacementScope (UnityGuiSessionReplacementScope sessionReplacementScope)
        {
            switch (sessionReplacementScope)
            {
                case UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession:
                case UnityGuiSessionReplacementScope.AnyCurrentProcessSession:
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sessionReplacementScope), sessionReplacementScope, null);
            }
        }

        internal static bool CanReplaceActiveSession (
            UnityGuiSessionReplacementScope sessionReplacementScope,
            bool isMutationBusy)
        {
            return sessionReplacementScope == UnityGuiSessionReplacementScope.AnyCurrentProcessSession
                && !isMutationBusy;
        }

        private static bool TryAttachPreparedSession (
            StartingGuiBootstrapState state,
            UnityGuiSessionPersistence.PreparedSession preparedSession)
        {
            lock (SyncRoot)
            {
                if (!IsStartingGenerationOwnedWithoutLock(state))
                {
                    return false;
                }

                state.AttachPreparedSession(preparedSession);
                return true;
            }
        }

        private static bool TryAttachStartingResources (
            StartingGuiBootstrapState state,
            IUnityIpcServer server,
            IDisposable unityLogCaptureService,
            IServiceProvider serviceProvider)
        {
            lock (SyncRoot)
            {
                if (!IsStartingGenerationOwnedWithoutLock(state))
                {
                    return false;
                }

                state.AttachResources(server, unityLogCaptureService, serviceProvider);
                return true;
            }
        }

        private static bool TryAttachLifecycleSidecarWriter (
            StartingGuiBootstrapState state,
            UnityLifecycleSidecarWriter lifecycleSidecarWriter)
        {
            lock (SyncRoot)
            {
                if (!IsStartingGenerationOwnedWithoutLock(state))
                {
                    return false;
                }

                state.AttachLifecycleSidecarWriter(lifecycleSidecarWriter);
                return true;
            }
        }

        private static void StartUnityLogCaptureForStartingGeneration (
            StartingGuiBootstrapState state,
            UnityLogCaptureService unityLogCaptureService)
        {
            lock (SyncRoot)
            {
                if (!IsStartingGenerationOwnedWithoutLock(state))
                {
                    throw CreateStartingGenerationCancellation(state);
                }

                unityLogCaptureService.Start();
            }
        }

        private static Task<UnityGuiSessionRegistration> BeginTrackedSessionPublication (
            StartingGuiBootstrapState state,
            UnityGuiSessionPersistence.PreparedSession preparedSession)
        {
            lock (SyncRoot)
            {
                if (!IsStartingGenerationOwnedWithoutLock(state))
                {
                    throw CreateStartingGenerationCancellation(state);
                }

                var publicationTask = UnityGuiSessionPersistence.PublishAsync(
                    preparedSession,
                    state.CancellationToken);
                state.AttachSessionPublicationTask(publicationTask);
                return publicationTask;
            }
        }

        private static void EnsureStartingGenerationOwnership (StartingGuiBootstrapState state)
        {
            lock (SyncRoot)
            {
                if (IsStartingGenerationOwnedWithoutLock(state))
                {
                    return;
                }
            }

            throw CreateStartingGenerationCancellation(state);
        }

        private static bool IsStartingGenerationOwnedWithoutLock (StartingGuiBootstrapState state)
        {
            return ReferenceEquals(startingState, state)
                && state.IsUnclaimed
                && !state.CancellationToken.IsCancellationRequested;
        }

        private static OperationCanceledException CreateStartingGenerationCancellation (
            StartingGuiBootstrapState state)
        {
            return new OperationCanceledException(
                "The GUI daemon startup generation no longer owns publication.",
                state.CancellationToken);
        }

        private static void TransferStartingGenerationToActive (
            StartingGuiBootstrapState state,
            ActiveGuiBootstrapState nextState)
        {
            lock (SyncRoot)
            {
                if (!IsStartingGenerationOwnedWithoutLock(state)
                    || !state.TryClaimActiveTransfer())
                {
                    throw CreateStartingGenerationCancellation(state);
                }

                startingState = null;
                activeState = nextState;
            }
        }

        private static bool IsActiveGeneration (ActiveGuiBootstrapState state)
        {
            if (state == null)
            {
                return false;
            }

            lock (SyncRoot)
            {
                return ReferenceEquals(activeState, state)
                    || ReferenceEquals(stoppingState, state);
            }
        }

        private static void CompleteUnusedStartingGeneration (StartingGuiBootstrapState state)
        {
            var claimed = false;
            lock (SyncRoot)
            {
                if (ReferenceEquals(startingState, state)
                    && state.TryClaimNormalCleanup())
                {
                    claimed = true;
                }
            }

            if (!claimed)
            {
                return;
            }

            state.CancelAndDisposeInBackground();
            state.ReleaseManagedResourcesOnce(() =>
                ReleaseResourcesForEditorLifecycleEvent(
                    null,
                    state.Server,
                    state.UnityLogCaptureService,
                    state.ServiceProvider,
                    state.DaemonLogger,
                    deleteSession: false));
            state.SchedulePreparedSessionCleanupAfterPublicationTerminates("after abandoned startup");
            ClearStartingState(state);
        }

        private static async Task CleanupTrackedFailedStartAsync (StartingGuiBootstrapState state)
        {
            lock (SyncRoot)
            {
                if (!ReferenceEquals(startingState, state) || !state.IsUnclaimed)
                {
                    return;
                }
            }

            var stoppedSafely = true;
            if (state.Server != null)
            {
                try
                {
                    await state.Server.StopAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    stoppedSafely = false;
                    state.DaemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        FormatCleanupFailureMessage("GUI IPC server stop", "after failed startup", exception));
                }
            }

            var writerStoppedSafely = await InvalidateAndStopLifecycleSidecarWriterAsync(
                state.LifecycleSidecarWriter,
                state.DaemonLogger,
                "after failed startup server stop");
            stoppedSafely = stoppedSafely && writerStoppedSafely;

            state.SchedulePreparedSessionCleanupAfterPublicationTerminates("after failed startup server stop");
            await state.PreparedSessionFinalization;
            state.LogPreparedSessionFinalizationWarning();

            var ownsCleanup = false;
            lock (SyncRoot)
            {
                if (ReferenceEquals(startingState, state)
                    && state.TryClaimNormalCleanup())
                {
                    ownsCleanup = true;
                    if (!stoppedSafely)
                    {
                        startingState = null;
                        isBootstrapRestartBlocked = true;
                        failedStartResourceRetention = new FailedStartResourceRetention(
                            state.Server,
                            state.UnityLogCaptureService,
                            state.ServiceProvider,
                            state.DaemonLogger,
                            state.LifecycleSidecarWriter);
                    }
                }
            }

            if (!ownsCleanup)
            {
                return;
            }

            if (stoppedSafely)
            {
                state.ReleaseManagedResourcesOnce(() =>
                {
                    DisposeUnityLogCapture(
                        state.UnityLogCaptureService,
                        state.DaemonLogger,
                        "after failed startup");
                    DisposeServiceProvider(
                        state.ServiceProvider,
                        state.DaemonLogger,
                        "after failed startup");
                });
                ClearStartingState(state);
            }

            state.DisposeCancellationSource();
        }

        internal static async Task<(UnityGuiSessionRegistration Registration, IUnityIpcServerPublicationFence PublicationFence)> StartServerAndPublishSessionAsync (
            IUnityIpcServer server,
            IpcEndpoint endpoint,
            CancellationToken cancellationToken,
            Action validateGenerationOwnership,
            Func<Task<UnityGuiSessionRegistration>> publishSession)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (validateGenerationOwnership == null)
            {
                throw new ArgumentNullException(nameof(validateGenerationOwnership));
            }

            if (publishSession == null)
            {
                throw new ArgumentNullException(nameof(publishSession));
            }

            cancellationToken.ThrowIfCancellationRequested();
            validateGenerationOwnership();
            var publicationFence = await server.StartAsync(endpoint, cancellationToken);
            try
            {
                publicationFence.ThrowIfGenerationTerminated();
                cancellationToken.ThrowIfCancellationRequested();
                validateGenerationOwnership();
                var registration = await publishSession();
                publicationFence.ThrowIfGenerationTerminated();
                cancellationToken.ThrowIfCancellationRequested();
                validateGenerationOwnership();
                return (registration, publicationFence);
            }
            catch
            {
                publicationFence.Dispose();
                throw;
            }
        }

        /// <summary> Stops the active GUI daemon session registration when one exists. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by caller lifecycle. </param>
        /// <returns> A task that completes after active resources have been released. </returns>
        public static async Task StopAsync (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LifecycleGate.WaitAsync(cancellationToken);
            try
            {
                ActiveGuiBootstrapState capturedState;
                lock (SyncRoot)
                {
                    capturedState = activeState ?? stoppingState;
                }

                if (capturedState == null)
                {
                    return;
                }

                await StopStateAsync(
                    capturedState,
                    requestProcessExit: false,
                    deleteSession: true,
                    trackStoppedMutationFence: true);
            }
            finally
            {
                LifecycleGate.Release();
            }
        }

        private static async Task MonitorAsync (ActiveGuiBootstrapState state)
        {
            try
            {
                var shutdownWaitTask = state.ShutdownSignal.WaitAsync(CancellationToken.None);
                var serverTerminationTask = state.Server.WaitForTerminationAsync(CancellationToken.None);
                var completedTask = await Task.WhenAny(shutdownWaitTask, serverTerminationTask);
                if (ReferenceEquals(completedTask, serverTerminationTask))
                {
                    try
                    {
                        await serverTerminationTask;
                        state.DaemonLogger.Warning(
                            DaemonLogCategories.Lifecycle,
                            "GUI IPC server loop terminated before shutdown signal.");
                    }
                    finally
                    {
                        // A faulted listener is also terminal. Always invalidate the published session
                        // and dispose the provider so status cannot keep reporting an unreachable daemon.
                        await StopFromMonitorAsync(state, requestProcessExit: false);
                    }

                    return;
                }

                await shutdownWaitTask;
                state.DaemonLogger.Info(
                    DaemonLogCategories.Lifecycle,
                    "GUI daemon shutdown signal received. Stopping IPC server and invalidating session.");
                await StopFromMonitorAsync(state, requestProcessExit: state.Registration.CanShutdownProcess);
            }
            catch (Exception exception)
            {
                state.DaemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "GUI daemon monitor failed.",
                    exception);
            }
        }

        private static async Task StopFromMonitorAsync (
            ActiveGuiBootstrapState state,
            bool requestProcessExit)
        {
            await LifecycleGate.WaitAsync(CancellationToken.None);
            try
            {
                await StopStateAsync(
                    state,
                    requestProcessExit,
                    deleteSession: true,
                    trackStoppedMutationFence: true);
            }
            finally
            {
                LifecycleGate.Release();
            }
        }

        private static bool MarkStoppingState (ActiveGuiBootstrapState state)
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(activeState, state))
                {
                    activeState = null;
                }

                if (stoppingState != null && !ReferenceEquals(stoppingState, state))
                {
                    return false;
                }

                stoppingState = state;
                return true;
            }
        }

        private static void ClearStoppingState (ActiveGuiBootstrapState state)
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(stoppingState, state))
                {
                    stoppingState = null;
                }
            }
        }

        private static async Task<bool> StopStateAsync (
            ActiveGuiBootstrapState state,
            bool requestProcessExit,
            bool deleteSession,
            bool trackStoppedMutationFence)
        {
            if (!state.TryBeginStop())
            {
                return state.WasStoppedSafely;
            }

            if (!MarkStoppingState(state))
            {
                state.CompleteStop(stoppedSafely: false);
                return false;
            }

            if (trackStoppedMutationFence)
            {
                TrackStoppedMutationFence(state.MutationLaneControl);
            }

            var stoppedSafely = await ReleaseResourcesAsync(
                state.Registration,
                state.Server,
                state.LifecycleSidecarWriter,
                state.UnityLogCaptureService,
                state.ServiceProvider,
                state.DaemonLogger,
                cleanupContext: null,
                deleteSession);
            state.CompleteStop(stoppedSafely);
            if (stoppedSafely)
            {
                ClearStoppingState(state);
            }

            if (trackStoppedMutationFence)
            {
                TrackStoppedMutationFence(state.MutationLaneControl);
            }

            if (requestProcessExit)
            {
                EditorApplication.Exit(0);
            }

            return stoppedSafely;
        }

        private static void TrackStoppedMutationFence (IUnityMutationExecutionState mutationExecutionState)
        {
            if (!mutationExecutionState.IsBusy)
            {
                return;
            }

            lock (SyncRoot)
            {
                stoppedMutationFence = mutationExecutionState;
            }
        }

        /// <summary> Releases resources that may have been acquired before GUI bootstrap startup failed. </summary>
        /// <param name="registration"> The persisted session registration when already written. </param>
        /// <param name="server"> The IPC server instance when already resolved. </param>
        /// <param name="unityLogCaptureService"> The Unity log capture service when already resolved. </param>
        /// <param name="serviceProvider"> The service provider when already built. </param>
        /// <param name="daemonLogger"> The logger used to report cleanup failures. </param>
        /// <returns> A task whose result is <see langword="true" /> when the listener terminated safely; otherwise, <see langword="false" />. </returns>
        internal static async Task<bool> CleanupFailedStartAsync (
            UnityGuiSessionRegistration registration,
            IUnityIpcServer server,
            IDisposable unityLogCaptureService,
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger)
        {
            return await ReleaseResourcesAsync(
                registration,
                server,
                null,
                unityLogCaptureService,
                serviceProvider,
                daemonLogger,
                cleanupContext: "after failed startup",
                deleteSession: true);
        }

        private static async Task<bool> ReleaseResourcesAsync (
            UnityGuiSessionRegistration registration,
            IUnityIpcServer server,
            UnityLifecycleSidecarWriter lifecycleSidecarWriter,
            IDisposable unityLogCaptureService,
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger,
            string cleanupContext,
            bool deleteSession)
        {
            if (daemonLogger == null)
            {
                throw new ArgumentNullException(nameof(daemonLogger));
            }

            var serverStoppedSafely = true;
            if (server != null)
            {
                try
                {
                    await server.StopAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    serverStoppedSafely = false;
                    daemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        FormatCleanupFailureMessage("GUI IPC server stop", cleanupContext, exception));
                }
            }

            var writerStoppedSafely = await InvalidateAndStopLifecycleSidecarWriterAsync(
                lifecycleSidecarWriter,
                daemonLogger,
                cleanupContext);

            DeleteSession(registration, daemonLogger, cleanupContext, deleteSession);
            if (serverStoppedSafely)
            {
                DisposeUnityLogCapture(unityLogCaptureService, daemonLogger, cleanupContext);
                DisposeServiceProvider(serviceProvider, daemonLogger, cleanupContext);
            }

            return serverStoppedSafely && writerStoppedSafely;
        }

        /// <summary> Releases resources from Unity lifecycle callbacks without blocking editor close or domain reload. </summary>
        /// <param name="registration"> The persisted session registration when available. </param>
        /// <param name="server"> The IPC server instance when available. </param>
        /// <param name="unityLogCaptureService"> The Unity log capture service when available. </param>
        /// <param name="serviceProvider"> The service provider when available. </param>
        /// <param name="daemonLogger"> The logger used to report cleanup failures. </param>
        /// <param name="deleteSession"> Whether persisted session metadata should be deleted. </param>
        internal static void ReleaseResourcesForEditorLifecycleEvent (
            UnityGuiSessionRegistration registration,
            IUnityIpcServer server,
            IDisposable unityLogCaptureService,
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger,
            bool deleteSession)
        {
            if (daemonLogger == null)
            {
                throw new ArgumentNullException(nameof(daemonLogger));
            }

            const string CleanupContext = "during editor lifecycle event";
            ReleaseServerForEditorLifecycleEvent(server, daemonLogger, CleanupContext);
            ReleaseManagedResourcesForEditorLifecycleEvent(
                registration,
                unityLogCaptureService,
                serviceProvider,
                daemonLogger,
                deleteSession);
        }

        private static void ReleaseServerForEditorLifecycleEvent (
            IUnityIpcServer server,
            IDaemonLogger daemonLogger,
            string cleanupContext)
        {
            if (daemonLogger == null)
            {
                throw new ArgumentNullException(nameof(daemonLogger));
            }

            // NOTE:
            // EditorApplication.quitting and beforeAssemblyReload are synchronous Unity main-thread callbacks.
            // They must release OS transport handles on this callback thread, but must not wait for accepted
            // connection tasks. Domain reload can tear down those tasks while Unity is rebuilding assemblies;
            // joining them here can block the next GUI daemon bootstrap.
            if (server != null)
            {
                try
                {
                    server.ReleaseForEditorLifecycleEvent();
                }
                catch (Exception exception)
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        FormatCleanupFailureMessage("GUI IPC server lifecycle release", cleanupContext, exception));
                }
            }
        }

        private static void ReleaseManagedResourcesForEditorLifecycleEvent (
            UnityGuiSessionRegistration registration,
            IDisposable unityLogCaptureService,
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger,
            bool deleteSession)
        {
            DisposeUnityLogCapture(unityLogCaptureService, daemonLogger, "during editor lifecycle event");
            DeleteSession(
                registration,
                daemonLogger,
                "during editor lifecycle event",
                deleteSession);
            DisposeServiceProvider(serviceProvider, daemonLogger, "during editor lifecycle event");
        }

        private static void DisposeUnityLogCapture (
            IDisposable unityLogCaptureService,
            IDaemonLogger daemonLogger,
            string cleanupContext)
        {
            if (unityLogCaptureService == null)
            {
                return;
            }

            try
            {
                unityLogCaptureService.Dispose();
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    FormatCleanupFailureMessage("GUI Unity log capture disposal", cleanupContext, exception));
            }
        }

        private static void DeleteSession (
            UnityGuiSessionRegistration registration,
            IDaemonLogger daemonLogger,
            string cleanupContext,
            bool deleteSession)
        {
            if (registration == null || !deleteSession)
            {
                return;
            }

            try
            {
                UnityGuiSessionPersistence.Delete(registration);
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    FormatCleanupFailureMessage("GUI session cleanup", cleanupContext, exception));
            }
        }

        private static void DisposeServiceProvider (
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger,
            string cleanupContext)
        {
            if (serviceProvider is IDisposable disposableServiceProvider)
            {
                try
                {
                    disposableServiceProvider.Dispose();
                }
                catch (Exception exception)
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        FormatCleanupFailureMessage("GUI service provider disposal", cleanupContext, exception));
                }
            }
        }

        private static string FormatCleanupFailureMessage (
            string operation,
            string cleanupContext,
            Exception exception)
        {
            return string.IsNullOrEmpty(cleanupContext)
                ? $"{operation} failed. {exception.Message}"
                : $"{operation} {cleanupContext} failed. {exception.Message}";
        }

        private static void LogWarningBestEffort (
            IDaemonLogger daemonLogger,
            string message)
        {
            try
            {
                daemonLogger.Warning(DaemonLogCategories.Lifecycle, message);
            }
            catch (Exception)
            {
                // Diagnostics must not turn completed publication finalization into a lifecycle failure.
            }
        }

        private static void LogInfoBestEffort (
            IDaemonLogger daemonLogger,
            string message)
        {
            try
            {
                daemonLogger.Info(DaemonLogCategories.Lifecycle, message);
            }
            catch (Exception)
            {
                // Diagnostics must not turn a completed publication into a lifecycle failure.
            }
        }

        private static void EnsureEditorLifecycleSubscriptions ()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= StopForDomainReloadSynchronously;
            EditorApplication.playModeStateChanged -= PersistLifecycleSidecarOnPlayModeStateChanged;
            EditorApplication.update -= PersistLifecycleSidecarOnEditorUpdate;
            EditorApplication.quitting -= StopSynchronously;
            AssemblyReloadEvents.beforeAssemblyReload += StopForDomainReloadSynchronously;
            EditorApplication.playModeStateChanged += PersistLifecycleSidecarOnPlayModeStateChanged;
            EditorApplication.update += PersistLifecycleSidecarOnEditorUpdate;
            EditorApplication.quitting += StopSynchronously;
        }

        private static void PersistLifecycleSidecarOnPlayModeStateChanged (PlayModeStateChange _)
        {
            PersistActiveLifecycleSidecar(force: true);
        }

        private static void PersistLifecycleSidecarOnEditorUpdate ()
        {
            PersistActiveLifecycleSidecar(force: false);
        }

        private static void PersistActiveLifecycleSidecar (bool force)
        {
            ActiveGuiBootstrapState capturedState;
            lock (SyncRoot)
            {
                capturedState = activeState;
            }

            if (capturedState == null || capturedState.ShutdownSignal.IsSignaled)
            {
                return;
            }

            if (capturedState.LifecycleSidecarWriter.TryConsumeFailure(out var failureMessage))
            {
                capturedState.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI lifecycle sidecar refresh failed. Retrying in the background. {failureMessage}");
            }

            var now = DateTimeOffset.UtcNow;
            var lastScheduledAtUtc = capturedState.LifecycleSidecarWriter.LastScheduledAtUtc;
            if (!force
                && lastScheduledAtUtc.HasValue
                && now - lastScheduledAtUtc.Value < DaemonLifecycleObservationTimings.SidecarRefreshInterval)
            {
                return;
            }

            try
            {
                // NOTE:
                // CLI status commands may time out while Unity is in Play Mode or recovering, but Unity main-thread
                // callbacks still own the authoritative lifecycle snapshot. Keep the sidecar fresh enough to serve as
                // the read-only observation path without moving Unity API access off the main thread.
                var snapshot = capturedState.ReadinessGate.CaptureSnapshot() with
                {
                    ObservedAtUtc = now,
                };
                _ = capturedState.LifecycleSidecarWriter.TryEnqueue(
                    snapshot,
                    now,
                    out _);
            }
            catch (Exception exception)
            {
                capturedState.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI lifecycle sidecar refresh failed. {exception.Message}");
            }
        }

        private static void StopSynchronously ()
        {
            ActiveGuiBootstrapState capturedState;
            StartingGuiBootstrapState capturedStartingState;
            FailedStartResourceRetention capturedFailedStartResources;
            lock (SyncRoot)
            {
                capturedState = activeState ?? stoppingState;
                capturedStartingState = ClaimStartingStateForEditorLifecycleEventWithoutLock();
                capturedFailedStartResources = failedStartResourceRetention;
                activeState = null;
                stoppingState = null;
                failedStartResourceRetention = null;
            }

            ReleaseStartingStateForEditorLifecycleEvent(capturedStartingState);

            if (capturedState != null)
            {
                _ = capturedState.TryBeginStop();
                ReleaseServerForEditorLifecycleEvent(
                    capturedState.Server,
                    capturedState.DaemonLogger,
                    "during Unity Editor quit");
                TryInvalidateAndStopLifecycleSidecarWriterForEditorLifecycleEvent(
                    capturedState.LifecycleSidecarWriter,
                    capturedState.DaemonLogger,
                    "during Unity Editor quit");

                ReleaseManagedResourcesForEditorLifecycleEvent(
                    capturedState.Registration,
                    capturedState.UnityLogCaptureService,
                    capturedState.ServiceProvider,
                    capturedState.DaemonLogger,
                    deleteSession: true);
            }

            ReleaseFailedStartResourcesForEditorLifecycleEvent(capturedFailedStartResources);
        }

        private static void StopForDomainReloadSynchronously ()
        {
            ActiveGuiBootstrapState capturedState;
            StartingGuiBootstrapState capturedStartingState;
            FailedStartResourceRetention capturedFailedStartResources;
            lock (SyncRoot)
            {
                capturedState = activeState ?? stoppingState;
                capturedStartingState = ClaimStartingStateForEditorLifecycleEventWithoutLock();
                capturedFailedStartResources = failedStartResourceRetention;
                activeState = null;
                stoppingState = null;
                failedStartResourceRetention = null;
            }

            ReleaseStartingStateForEditorLifecycleEvent(capturedStartingState);

            if (capturedState != null)
            {
                var recoverySnapshotScheduled = TryScheduleRecoveryLifecycleSidecarForDomainReload(
                    capturedState,
                    out var recoverySnapshotVersion);
                _ = capturedState.TryBeginStop();
                ReleaseServerForEditorLifecycleEvent(
                    capturedState.Server,
                    capturedState.DaemonLogger,
                    "before domain reload");
                var recoverySnapshotFlushed = recoverySnapshotScheduled
                    && TryFlushRecoveryLifecycleSidecarForDomainReload(
                        capturedState,
                        recoverySnapshotVersion);
                if (recoverySnapshotFlushed)
                {
                    TryStopLifecycleSidecarWriterForEditorLifecycleEvent(
                        capturedState.LifecycleSidecarWriter,
                        capturedState.DaemonLogger,
                        "after recovery flush before domain reload");
                }
                else
                {
                    TryInvalidateAndStopLifecycleSidecarWriterForEditorLifecycleEvent(
                        capturedState.LifecycleSidecarWriter,
                        capturedState.DaemonLogger,
                        "after recovery flush failure before domain reload");
                }

                ReleaseManagedResourcesForEditorLifecycleEvent(
                    capturedState.Registration,
                    capturedState.UnityLogCaptureService,
                    capturedState.ServiceProvider,
                    capturedState.DaemonLogger,
                    deleteSession: !recoverySnapshotFlushed);
            }

            ReleaseFailedStartResourcesForEditorLifecycleEvent(capturedFailedStartResources);
        }

        private static StartingGuiBootstrapState ClaimStartingStateForEditorLifecycleEventWithoutLock ()
        {
            var capturedStartingState = startingState;
            if (capturedStartingState == null
                || (!capturedStartingState.TryClaimEditorLifecycleRelease()
                    && !capturedStartingState.IsNormalCleanupClaimed))
            {
                return null;
            }

            startingState = null;
            return capturedStartingState;
        }

        internal static void ReleaseStartingStateForEditorLifecycleEvent (
            StartingGuiBootstrapState state)
        {
            if (state == null || !state.CanReleaseForEditorLifecycleEvent)
            {
                return;
            }

            state.CancelAndDisposeInBackground();
            state.ReleaseManagedResourcesOnce(() =>
            {
                ReleaseServerForEditorLifecycleEvent(
                    state.Server,
                    state.DaemonLogger,
                    "during starting-generation editor lifecycle release");
                TryInvalidateAndStopLifecycleSidecarWriterForEditorLifecycleEvent(
                    state.LifecycleSidecarWriter,
                    state.DaemonLogger,
                    "during starting-generation editor lifecycle release");

                ReleaseManagedResourcesForEditorLifecycleEvent(
                    null,
                    state.UnityLogCaptureService,
                    state.ServiceProvider,
                    state.DaemonLogger,
                    deleteSession: false);
            });
            state.SchedulePreparedSessionCleanupAfterPublicationTerminates(
                "during editor lifecycle event");
        }

        private static bool TryScheduleRecoveryLifecycleSidecarForDomainReload (
            ActiveGuiBootstrapState state,
            out long version)
        {
            try
            {
                var observedAtUtc = DateTimeOffset.UtcNow;
                var snapshot = state.ReadinessGate.CaptureSnapshot() with
                {
                    LifecycleState = IpcEditorLifecycleStateCodec.Recovering,
                    BlockingReason = UnityEditorExecutionReadinessPolicy.ResolveBlockingReason(IpcEditorLifecycleStateCodec.Recovering),
                    CanAcceptExecutionRequests = false,
                    ObservedAtUtc = observedAtUtc,
                };
                if (!state.LifecycleSidecarWriter.TryEnqueue(snapshot, observedAtUtc, out version))
                {
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                version = 0;
                state.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI lifecycle recovery sidecar capture failed before domain reload. {exception.Message}");
                return false;
            }
        }

        private static bool TryFlushRecoveryLifecycleSidecarForDomainReload (
            ActiveGuiBootstrapState state,
            long version)
        {
            try
            {
                using var flushCancellationSource = new CancellationTokenSource(
                    LifecycleSidecarReloadFlushTimeout);
                state.LifecycleSidecarWriter
                    .FlushAsync(version, flushCancellationSource.Token)
                    .GetAwaiter()
                    .GetResult();
                return true;
            }
            catch (Exception exception)
            {
                state.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI lifecycle recovery sidecar flush failed before domain reload. {exception.Message}");
                return false;
            }
        }

        private static async Task<bool> InvalidateAndStopLifecycleSidecarWriterAsync (
            UnityLifecycleSidecarWriter writer,
            IDaemonLogger daemonLogger,
            string cleanupContext)
        {
            if (writer == null)
            {
                return true;
            }

            using var cleanupCancellationSource = new CancellationTokenSource(
                LifecycleSidecarAsyncCleanupTimeout);
            try
            {
                await writer.InvalidateAndStopAsync(cleanupCancellationSource.Token);
                return true;
            }
            catch (OperationCanceledException) when (cleanupCancellationSource.IsCancellationRequested)
            {
                var effectiveCleanupContext = cleanupContext ?? "during normal stop";
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI lifecycle sidecar owned cleanup {effectiveCleanupContext} exceeded its {LifecycleSidecarAsyncCleanupTimeout.TotalMilliseconds:0}ms foreground deadline and continues in the background.");
                return true;
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    FormatCleanupFailureMessage(
                        "GUI lifecycle sidecar owned cleanup",
                        cleanupContext,
                        exception));
                return false;
            }
        }

        private static bool TryStopLifecycleSidecarWriterForEditorLifecycleEvent (
            UnityLifecycleSidecarWriter writer,
            IDaemonLogger daemonLogger,
            string cleanupContext)
        {
            if (writer == null)
            {
                return true;
            }

            using var stopCancellationSource = new CancellationTokenSource(
                LifecycleSidecarWriterStopTimeout);
            try
            {
                writer.StopAsync(stopCancellationSource.Token).GetAwaiter().GetResult();
                return true;
            }
            catch (OperationCanceledException) when (stopCancellationSource.IsCancellationRequested)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI lifecycle sidecar writer stop {cleanupContext} exceeded its {LifecycleSidecarWriterStopTimeout.TotalMilliseconds:0}ms lifecycle deadline and continues in the background.");
                return true;
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    FormatCleanupFailureMessage(
                        "GUI lifecycle sidecar writer bounded stop",
                        cleanupContext,
                        exception));
                return false;
            }
        }

        private static bool TryInvalidateAndStopLifecycleSidecarWriterForEditorLifecycleEvent (
            UnityLifecycleSidecarWriter writer,
            IDaemonLogger daemonLogger,
            string cleanupContext)
        {
            if (writer == null)
            {
                return true;
            }

            using var cleanupCancellationSource = new CancellationTokenSource(
                LifecycleSidecarWriterStopTimeout);
            try
            {
                writer
                    .InvalidateAndStopAsync(cleanupCancellationSource.Token)
                    .GetAwaiter()
                    .GetResult();
                return true;
            }
            catch (OperationCanceledException) when (cleanupCancellationSource.IsCancellationRequested)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI lifecycle sidecar owned cleanup {cleanupContext} exceeded its {LifecycleSidecarWriterStopTimeout.TotalMilliseconds:0}ms lifecycle deadline and continues in the background.");
                return true;
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    FormatCleanupFailureMessage(
                        "GUI lifecycle sidecar owned cleanup",
                        cleanupContext,
                        exception));
                return false;
            }
        }

        private static void ClearStartingState (StartingGuiBootstrapState state)
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(startingState, state))
                {
                    startingState = null;
                }
            }
        }

        private static void ReleaseFailedStartResourcesForEditorLifecycleEvent (
            FailedStartResourceRetention retainedResources)
        {
            if (retainedResources == null)
            {
                return;
            }

            ReleaseServerForEditorLifecycleEvent(
                retainedResources.Server,
                retainedResources.DaemonLogger,
                "during failed-start editor lifecycle release");
            TryInvalidateAndStopLifecycleSidecarWriterForEditorLifecycleEvent(
                retainedResources.LifecycleSidecarWriter,
                retainedResources.DaemonLogger,
                "during failed-start editor lifecycle release");
            ReleaseManagedResourcesForEditorLifecycleEvent(
                null,
                retainedResources.UnityLogCaptureService,
                retainedResources.ServiceProvider,
                retainedResources.DaemonLogger,
                deleteSession: false);
        }

        internal sealed class StartingGuiBootstrapState
        {
            private const int CompletionOwnerActiveState = 1;

            private const int CompletionOwnerEditorLifecycle = 2;

            private const int CompletionOwnerNormalCleanup = 3;

            private readonly CancellationTokenSource cancellationSource;

            private readonly CancellationToken cancellationToken;

            private readonly object managedResourceReleaseSyncRoot = new object();

            private readonly object preparedSessionWarningSyncRoot = new object();

            private readonly TaskCompletionSource<bool> preparedSessionFinalizationCompletionSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> cancellationFinalizationCompletionSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int completionOwner;

            private int cancellationFinalizationOwner;

            private int preparedSessionFinalizationStarted;

            private bool managedResourcesReleased;

            private Task<UnityGuiSessionRegistration> sessionPublicationTask;

            private string preparedSessionFinalizationWarning;

            public StartingGuiBootstrapState (
                CancellationToken callerCancellationToken,
                Guid editorInstanceId,
                IDaemonLogger daemonLogger)
            {
                if (editorInstanceId == Guid.Empty)
                {
                    throw new ArgumentException("Editor instance identifier must not be empty.", nameof(editorInstanceId));
                }

                CallerCancellationToken = callerCancellationToken;
                EditorInstanceId = editorInstanceId;
                DaemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
                cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken);
                cancellationToken = cancellationSource.Token;
            }

            public CancellationToken CallerCancellationToken { get; }

            public Guid EditorInstanceId { get; }

            public CancellationToken CancellationToken => cancellationToken;

            public IDaemonLogger DaemonLogger { get; }

            public UnityGuiSessionPersistence.PreparedSession PreparedSession { get; private set; }

            public UnityGuiSessionRegistration Registration { get; private set; }

            public IUnityIpcServer Server { get; private set; }

            public IDisposable UnityLogCaptureService { get; private set; }

            public IServiceProvider ServiceProvider { get; private set; }

            public UnityLifecycleSidecarWriter LifecycleSidecarWriter { get; private set; }

            public Task PreparedSessionFinalization => preparedSessionFinalizationCompletionSource.Task;

            public Task CancellationFinalization => cancellationFinalizationCompletionSource.Task;

            public bool IsUnclaimed => Volatile.Read(ref completionOwner) == 0;

            public bool IsNormalCleanupClaimed =>
                Volatile.Read(ref completionOwner) == CompletionOwnerNormalCleanup;

            public bool CanReleaseForEditorLifecycleEvent
            {
                get
                {
                    var owner = Volatile.Read(ref completionOwner);
                    return owner == CompletionOwnerEditorLifecycle
                        || owner == CompletionOwnerNormalCleanup;
                }
            }

            public void AttachPreparedSession (UnityGuiSessionPersistence.PreparedSession preparedSession)
            {
                if (!IsUnclaimed)
                {
                    throw new InvalidOperationException("The GUI daemon startup generation no longer owns resources.");
                }

                PreparedSession = preparedSession ?? throw new ArgumentNullException(nameof(preparedSession));
                Registration = preparedSession.Registration;
            }

            public void AttachResources (
                IUnityIpcServer server,
                IDisposable unityLogCaptureService,
                IServiceProvider serviceProvider)
            {
                if (!IsUnclaimed)
                {
                    throw new InvalidOperationException("The GUI daemon startup generation no longer owns resources.");
                }

                Server = server ?? throw new ArgumentNullException(nameof(server));
                UnityLogCaptureService = unityLogCaptureService ?? throw new ArgumentNullException(nameof(unityLogCaptureService));
                ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            }

            public void AttachLifecycleSidecarWriter (UnityLifecycleSidecarWriter lifecycleSidecarWriter)
            {
                if (!IsUnclaimed)
                {
                    throw new InvalidOperationException("The GUI daemon startup generation no longer owns resources.");
                }

                if (LifecycleSidecarWriter != null)
                {
                    throw new InvalidOperationException("A lifecycle sidecar writer is already attached.");
                }

                LifecycleSidecarWriter = lifecycleSidecarWriter
                    ?? throw new ArgumentNullException(nameof(lifecycleSidecarWriter));
            }

            public void AttachSessionPublicationTask (Task<UnityGuiSessionRegistration> publicationTask)
            {
                if (!IsUnclaimed)
                {
                    throw new InvalidOperationException("The GUI daemon startup generation no longer owns publication.");
                }

                if (PreparedSession == null)
                {
                    throw new InvalidOperationException("A prepared GUI session is required before publication starts.");
                }

                if (sessionPublicationTask != null)
                {
                    throw new InvalidOperationException("GUI session publication has already started.");
                }

                sessionPublicationTask = publicationTask ?? throw new ArgumentNullException(nameof(publicationTask));
            }

            public bool TryClaimActiveTransfer ()
            {
                return TryClaimCompletion(CompletionOwnerActiveState);
            }

            public bool TryClaimEditorLifecycleRelease ()
            {
                return TryClaimCompletion(CompletionOwnerEditorLifecycle);
            }

            public bool TryClaimNormalCleanup ()
            {
                return TryClaimCompletion(CompletionOwnerNormalCleanup);
            }

            public void ReleaseManagedResourcesOnce (Action releaseResources)
            {
                if (releaseResources == null)
                {
                    throw new ArgumentNullException(nameof(releaseResources));
                }

                lock (managedResourceReleaseSyncRoot)
                {
                    if (managedResourcesReleased)
                    {
                        return;
                    }

                    releaseResources();
                    managedResourcesReleased = true;
                }
            }

            public void CancelAndDisposeInBackground ()
            {
                if (Interlocked.CompareExchange(ref cancellationFinalizationOwner, 1, 0) != 0)
                {
                    return;
                }

                _ = Task.Run(() =>
                {
                    try
                    {
                        cancellationSource.Cancel();
                    }
                    catch (Exception exception)
                    {
                        RecordPreparedSessionFinalizationWarning(
                            $"GUI daemon startup cancellation callback failed. {exception.Message}");
                    }
                    finally
                    {
                        try
                        {
                            cancellationSource.Dispose();
                        }
                        catch (Exception exception)
                        {
                            RecordPreparedSessionFinalizationWarning(
                                $"GUI daemon startup cancellation source disposal failed. {exception.Message}");
                        }

                        cancellationFinalizationCompletionSource.TrySetResult(true);
                    }
                });
            }

            public void EnsurePreparedSessionPublicationReadyForCommit ()
            {
                if (sessionPublicationTask == null
                    || !sessionPublicationTask.IsCompleted
                    || sessionPublicationTask.IsCanceled
                    || sessionPublicationTask.IsFaulted)
                {
                    throw new InvalidOperationException(
                        "GUI session publication must complete successfully before active ownership commits.");
                }

                if (PreparedSession == null
                    || Volatile.Read(ref preparedSessionFinalizationStarted) != 0)
                {
                    throw new InvalidOperationException(
                        "GUI session publication ownership was finalized before active ownership committed.");
                }
            }

            public void ReleasePreparedSessionAfterSuccessfulPublication ()
            {
                if (Interlocked.Exchange(ref preparedSessionFinalizationStarted, 1) != 0)
                {
                    LogWarningBestEffort(
                        DaemonLogger,
                        "Prepared GUI session lease was already finalized after active publication.");
                    return;
                }

                try
                {
                    PreparedSession?.Dispose();
                }
                catch (Exception exception)
                {
                    LogWarningBestEffort(
                        DaemonLogger,
                        $"Prepared GUI session lease release failed after successful publication. {exception.Message}");
                }
                finally
                {
                    preparedSessionFinalizationCompletionSource.TrySetResult(true);
                }
            }

            public void SchedulePreparedSessionCleanupAfterPublicationTerminates (string cleanupContext)
            {
                if (Interlocked.Exchange(ref preparedSessionFinalizationStarted, 1) != 0)
                {
                    return;
                }

                var publicationTask = sessionPublicationTask;
                if (publicationTask == null || publicationTask.IsCompleted)
                {
                    FinalizeFailedPreparedSession(cleanupContext, publicationTask);
                    return;
                }

                _ = publicationTask.ContinueWith(
                    completedTask => FinalizeFailedPreparedSession(cleanupContext, completedTask),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            public void LogPreparedSessionFinalizationWarning ()
            {
                string warning;
                lock (preparedSessionWarningSyncRoot)
                {
                    warning = preparedSessionFinalizationWarning;
                    preparedSessionFinalizationWarning = null;
                }

                if (!string.IsNullOrEmpty(warning))
                {
                    LogWarningBestEffort(DaemonLogger, warning);
                }
            }

            public void DisposeCancellationSource ()
            {
                if (Interlocked.CompareExchange(ref cancellationFinalizationOwner, 2, 0) != 0)
                {
                    return;
                }

                try
                {
                    cancellationSource.Dispose();
                }
                finally
                {
                    cancellationFinalizationCompletionSource.TrySetResult(true);
                }
            }

            private bool TryClaimCompletion (int owner)
            {
                return Interlocked.CompareExchange(ref completionOwner, owner, 0) == 0;
            }

            private void FinalizeFailedPreparedSession (
                string cleanupContext,
                Task publicationTask)
            {
                _ = publicationTask?.Exception;
                if (PreparedSession == null)
                {
                    preparedSessionFinalizationCompletionSource.TrySetResult(true);
                    return;
                }

                try
                {
                    UnityGuiSessionPersistence.DeleteOwnedSessionBeforeLeaseRelease(PreparedSession);
                }
                catch (Exception exception)
                {
                    RecordPreparedSessionFinalizationWarning(
                        FormatCleanupFailureMessage(
                            "GUI session cleanup under publication lease",
                            cleanupContext,
                            exception));
                }
                finally
                {
                    try
                    {
                        PreparedSession.Dispose();
                    }
                    catch (Exception exception)
                    {
                        RecordPreparedSessionFinalizationWarning(
                            FormatCleanupFailureMessage(
                                "Prepared GUI session lease release",
                                cleanupContext,
                                exception));
                    }
                    finally
                    {
                        preparedSessionFinalizationCompletionSource.TrySetResult(true);
                    }
                }
            }

            private void RecordPreparedSessionFinalizationWarning (string warning)
            {
                lock (preparedSessionWarningSyncRoot)
                {
                    preparedSessionFinalizationWarning = string.IsNullOrEmpty(preparedSessionFinalizationWarning)
                        ? warning
                        : $"{preparedSessionFinalizationWarning} {warning}";
                }
            }
        }

        private sealed class FailedStartResourceRetention
        {
            public FailedStartResourceRetention (
                IUnityIpcServer server,
                IDisposable unityLogCaptureService,
                IServiceProvider serviceProvider,
                IDaemonLogger daemonLogger,
                UnityLifecycleSidecarWriter lifecycleSidecarWriter)
            {
                Server = server ?? throw new ArgumentNullException(nameof(server));
                UnityLogCaptureService = unityLogCaptureService;
                ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                DaemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
                LifecycleSidecarWriter = lifecycleSidecarWriter;
            }

            public IUnityIpcServer Server { get; }

            public IDisposable UnityLogCaptureService { get; }

            public IServiceProvider ServiceProvider { get; }

            public IDaemonLogger DaemonLogger { get; }

            public UnityLifecycleSidecarWriter LifecycleSidecarWriter { get; }
        }

        private sealed class ActiveGuiBootstrapState
        {
            private int stopStarted;

            private int stopSafety;

            public ActiveGuiBootstrapState (
                UnityGuiSessionRegistration registration,
                IUnityIpcServer server,
                IDaemonShutdownSignal shutdownSignal,
                UnityLogCaptureService unityLogCaptureService,
                IServiceProvider serviceProvider,
                IDaemonLogger daemonLogger,
                IUnityEditorReadinessGate readinessGate,
                IUnityMutationLaneControl mutationLaneControl,
                UnityLifecycleSidecarWriter lifecycleSidecarWriter)
            {
                Registration = registration ?? throw new ArgumentNullException(nameof(registration));
                Server = server ?? throw new ArgumentNullException(nameof(server));
                ShutdownSignal = shutdownSignal ?? throw new ArgumentNullException(nameof(shutdownSignal));
                UnityLogCaptureService = unityLogCaptureService ?? throw new ArgumentNullException(nameof(unityLogCaptureService));
                ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                DaemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
                ReadinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
                MutationLaneControl = mutationLaneControl ?? throw new ArgumentNullException(nameof(mutationLaneControl));
                LifecycleSidecarWriter = lifecycleSidecarWriter
                    ?? throw new ArgumentNullException(nameof(lifecycleSidecarWriter));
            }

            public UnityGuiSessionRegistration Registration { get; }

            public IUnityIpcServer Server { get; }

            public IDaemonShutdownSignal ShutdownSignal { get; }

            public UnityLogCaptureService UnityLogCaptureService { get; }

            public IServiceProvider ServiceProvider { get; }

            public IDaemonLogger DaemonLogger { get; }

            public IUnityEditorReadinessGate ReadinessGate { get; }

            public IUnityMutationLaneControl MutationLaneControl { get; }

            public UnityLifecycleSidecarWriter LifecycleSidecarWriter { get; }

            public bool WasStoppedSafely => Volatile.Read(ref stopSafety) == 1;

            public bool TryBeginStop ()
            {
                return Interlocked.Exchange(ref stopStarted, 1) == 0;
            }

            public void CompleteStop (bool stoppedSafely)
            {
                Volatile.Write(ref stopSafety, stoppedSafely ? 1 : 2);
            }
        }
    }
}
