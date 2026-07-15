using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;
using MackySoft.Ucli.Unity.Runtime;
using Microsoft.Extensions.DependencyInjection;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Hosts the GUI-side supervisor endpoint that survives daemon endpoint shutdown. </summary>
    internal static class UnityGuiSupervisorBootstrap
    {
        private static readonly object SyncRoot = new object();

        private static readonly UnityGuiSupervisorRestartScheduler RestartScheduler =
            new UnityGuiSupervisorRestartScheduler(
                static () => EditorApplication.timeSinceStartup,
                static callback => EditorApplication.update += callback,
                static callback => EditorApplication.update -= callback,
                CanScheduleRestart,
                StartScheduledRestart);

        private static ActiveGuiSupervisorState activeState;

        private static StartingGuiSupervisorState startingState;

        private static ActiveGuiSupervisorState stoppingState;

        private static FailedStartResourceRetention failedStartResourceRetention;

        private static bool isBootstrapRestartBlocked;

        private static bool isEditorLifecycleStopping;

        public static Task StartAsync ()
        {
            ActiveGuiSupervisorState capturedState;
            bool capturedRestartBlock;
            lock (SyncRoot)
            {
                capturedState = activeState;
                capturedRestartBlock = isBootstrapRestartBlocked
                    || isEditorLifecycleStopping
                    || startingState != null
                    || stoppingState != null;
            }

            if (capturedState != null || capturedRestartBlock)
            {
                return Task.CompletedTask;
            }

            var daemonLogger = new DaemonLogger(
                new DaemonLogRingBuffer(),
                UnityMainThreadDaemonConsoleLogSink.CaptureCurrent());
            var nextStartingState = new StartingGuiSupervisorState(daemonLogger);
            lock (SyncRoot)
            {
                if (activeState != null
                    || startingState != null
                    || stoppingState != null
                    || isBootstrapRestartBlocked
                    || isEditorLifecycleStopping)
                {
                    nextStartingState.DisposeCancellationSource();
                    return Task.CompletedTask;
                }

                startingState = nextStartingState;
            }

            RestartScheduler.CancelPendingRestart();
            EnsureEditorLifecycleSubscriptions();
            return StartTrackedAsync(nextStartingState);
        }

        private static async Task StartTrackedAsync (StartingGuiSupervisorState state)
        {
            try
            {
                EnsureStartingGenerationOwnership(state);
                lock (SyncRoot)
                {
                    if (activeState != null
                        || stoppingState != null
                        || isBootstrapRestartBlocked
                        || isEditorLifecycleStopping)
                    {
                        return;
                    }
                }

                var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
                var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectRoot);
                var projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectRoot);
                var endpoint = UcliIpcEndpointResolver.ResolveGuiSupervisorEndpoint(storageRoot, projectFingerprint);
                var sessionToken = IpcSessionToken.CreateRandom();
                if (!TryAttachStartingIdentity(
                        state,
                        storageRoot,
                        projectFingerprint,
                        sessionToken))
                {
                    throw CreateStartingGenerationCancellation(state);
                }

                var services = new ServiceCollection();
                services.AddUnityGuiSupervisorHostServices(
                    new ExactSessionTokenValidator(sessionToken),
                    projectFingerprint,
                    endpoint,
                    state.DaemonLogger);
                var serviceProvider = services.BuildServiceProvider();
                var server = serviceProvider.GetRequiredService<IUnityIpcServer>();
                var controlPlaneRequestLifetime = serviceProvider
                    .GetRequiredService<IUnityControlPlaneRequestLifetime>();
                if (!TryAttachStartingResources(
                        state,
                        server,
                        serviceProvider,
                        controlPlaneRequestLifetime))
                {
                    ReleaseUnattachedStartingResources(server, serviceProvider, state.DaemonLogger);
                    throw CreateStartingGenerationCancellation(state);
                }

                EnsureStartingGenerationOwnership(state);
                var publicationLease = await UnityGuiSupervisorPersistence.AcquirePublicationLeaseAsync(
                    storageRoot,
                    projectFingerprint,
                    state.CancellationToken);
                if (!TryAttachPublicationLease(state, publicationLease))
                {
                    publicationLease.Dispose();
                    throw CreateStartingGenerationCancellation(state);
                }

                EnsureStartingGenerationOwnership(state);
                using var publicationFence = await server.StartAsync(endpoint, state.CancellationToken);
                publicationFence.ThrowIfGenerationTerminated();
                EnsureStartingGenerationOwnership(state);
                var manifest = await BeginTrackedManifestPublication(
                    state,
                    publicationLease,
                    endpoint,
                    sessionToken);
                if (!TryValidatePublishedManifest(state, manifest))
                {
                    throw CreateStartingGenerationCancellation(state);
                }

                var nextState = new ActiveGuiSupervisorState(
                    sessionToken,
                    server,
                    serviceProvider,
                    controlPlaneRequestLifetime,
                    state.DaemonLogger,
                    storageRoot,
                    projectFingerprint);
                state.EnsureManifestPublicationReadyForCommit();
                state.DisposeCancellationSource();
                if (!publicationFence.TryCommitActiveOwnership(
                        () => TransferStartingGenerationToActive(state, nextState)))
                {
                    throw new InvalidOperationException(
                        "GUI supervisor listener terminated before its manifest publication could become active.");
                }

                try
                {
                    RestartScheduler.BeginGenerationStabilityObservation(nextState);
                }
                catch (Exception exception)
                {
                    LogWarningBestEffort(
                        state.DaemonLogger,
                        $"GUI supervisor stability observation failed. {exception.Message}");
                }

                state.ReleasePublicationLeaseAfterSuccessfulPublication();
                LogInfoBestEffort(
                    state.DaemonLogger,
                    $"uCLI GUI supervisor registered. storageRoot={storageRoot}, fingerprint={projectFingerprint}, endpoint={endpoint.Address}");
                _ = MonitorAsync(nextState);
            }
            catch (Exception exception)
            {
                var isLifecycleCancellation = exception is OperationCanceledException
                    && state.CancellationToken.IsCancellationRequested;
                if (!isLifecycleCancellation)
                {
                    state.DaemonLogger.Exception(
                        DaemonLogCategories.Lifecycle,
                        "uCLI GUI supervisor bootstrap failed.",
                        exception);
                }

                var mayRestart = await CleanupTrackedFailedStartAsync(state);
                if (mayRestart)
                {
                    RestartScheduler.ScheduleAfterSafeCleanup();
                }
                else if (state.HasDeferredResourceRelease)
                {
                    ObserveFault(RestartAfterDeferredFailedStartCleanupAsync(state));
                }
            }
            finally
            {
                CompleteUnusedStartingGeneration(state);
            }
        }

        private static bool TryAttachStartingIdentity (
            StartingGuiSupervisorState state,
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            IpcSessionToken sessionToken)
        {
            lock (SyncRoot)
            {
                if (!IsStartingGenerationOwnedWithoutLock(state))
                {
                    return false;
                }

                state.AttachIdentity(storageRoot, projectFingerprint, sessionToken);
                return true;
            }
        }

        private static bool TryAttachStartingResources (
            StartingGuiSupervisorState state,
            IUnityIpcServer server,
            IServiceProvider serviceProvider,
            IUnityControlPlaneRequestLifetime controlPlaneRequestLifetime)
        {
            lock (SyncRoot)
            {
                if (!IsStartingGenerationOwnedWithoutLock(state))
                {
                    return false;
                }

                state.AttachResources(
                    server,
                    serviceProvider,
                    controlPlaneRequestLifetime);
                return true;
            }
        }

        private static bool TryValidatePublishedManifest (
            StartingGuiSupervisorState state,
            GuiSupervisorManifestJsonContract manifest)
        {
            lock (SyncRoot)
            {
                if (!IsStartingGenerationOwnedWithoutLock(state))
                {
                    return false;
                }

                state.ValidateManifest(manifest);
                return true;
            }
        }

        private static bool TryAttachPublicationLease (
            StartingGuiSupervisorState state,
            UnityGuiSupervisorPersistence.PublicationLease publicationLease)
        {
            lock (SyncRoot)
            {
                if (!IsStartingGenerationOwnedWithoutLock(state))
                {
                    return false;
                }

                state.AttachPublicationLease(publicationLease);
                return true;
            }
        }

        private static Task<GuiSupervisorManifestJsonContract> BeginTrackedManifestPublication (
            StartingGuiSupervisorState state,
            UnityGuiSupervisorPersistence.PublicationLease publicationLease,
            IpcEndpoint endpoint,
            IpcSessionToken sessionToken)
        {
            lock (SyncRoot)
            {
                if (!IsStartingGenerationOwnedWithoutLock(state))
                {
                    throw CreateStartingGenerationCancellation(state);
                }

                var publicationTask = publicationLease.PublishAsync(
                        endpoint,
                        sessionToken,
                        DateTimeOffset.UtcNow,
                        state.CancellationToken)
                    .AsTask();
                state.AttachManifestPublicationTask(publicationTask);
                return publicationTask;
            }
        }

        private static void EnsureStartingGenerationOwnership (StartingGuiSupervisorState state)
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

        private static bool IsStartingGenerationOwnedWithoutLock (StartingGuiSupervisorState state)
        {
            return ReferenceEquals(startingState, state)
                && state.IsUnclaimed
                && !state.CancellationToken.IsCancellationRequested;
        }

        private static OperationCanceledException CreateStartingGenerationCancellation (
            StartingGuiSupervisorState state)
        {
            return new OperationCanceledException(
                "The GUI supervisor startup generation no longer owns publication.",
                state.CancellationToken);
        }

        private static void TransferStartingGenerationToActive (
            StartingGuiSupervisorState state,
            ActiveGuiSupervisorState nextState)
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

        private static void CompleteUnusedStartingGeneration (StartingGuiSupervisorState state)
        {
            var requiresTrackedCleanup = false;
            lock (SyncRoot)
            {
                if (!ReferenceEquals(startingState, state) || !state.IsUnclaimed)
                {
                    return;
                }

                requiresTrackedCleanup = state.Server != null;
                if (!requiresTrackedCleanup && !state.TryClaimNormalCleanup())
                {
                    return;
                }
            }

            state.Cancel();
            if (requiresTrackedCleanup)
            {
                ObserveFault(CleanupAbandonedStartingGenerationAsync(state));
                return;
            }

            state.ReleaseManagedResourcesOnce(() =>
                ReleaseUntrackedStartingResources(
                    state.Server,
                    state.ServiceProvider,
                    state.DaemonLogger));
            state.ScheduleManifestCleanupAfterPublicationTerminates("after abandoned startup");
            ClearStartingState(state);
            state.DisposeCancellationSource();
        }

        private static async Task CleanupAbandonedStartingGenerationAsync (
            StartingGuiSupervisorState state)
        {
            var releasedSafely = await CleanupTrackedFailedStartAsync(state);
            if (releasedSafely)
            {
                RestartScheduler.ScheduleAfterSafeCleanup();
                return;
            }

            if (state.HasDeferredResourceRelease)
            {
                await RestartAfterDeferredFailedStartCleanupAsync(state);
            }
        }

        private static async Task<bool> CleanupTrackedFailedStartAsync (StartingGuiSupervisorState state)
        {
            lock (SyncRoot)
            {
                if (!ReferenceEquals(startingState, state) || !state.IsUnclaimed)
                {
                    return false;
                }
            }

            var stoppedSafely = true;
            var retirementDeferred = false;
            Task retirementTask = null;
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
                        $"GUI supervisor failed-start server stop failed. {exception.Message}");
                }

                if (stoppedSafely)
                {
                    try
                    {
                        retirementTask = state.ControlPlaneRequestLifetime.WaitForRetirementAsync();
                        stoppedSafely = await UnityHostGenerationRetirementPolicy
                            .WaitWithinForegroundDeadlineAsync(retirementTask);
                        if (!stoppedSafely)
                        {
                            retirementDeferred = true;
                            state.DaemonLogger.Warning(
                                DaemonLogCategories.Lifecycle,
                                $"GUI supervisor failed-start control-plane request retirement exceeded its {UnityHostGenerationRetirementPolicy.ForegroundDeadline.TotalMilliseconds:0}ms foreground deadline.");
                        }
                    }
                    catch (Exception exception)
                    {
                        stoppedSafely = false;
                        state.DaemonLogger.Warning(
                            DaemonLogCategories.Lifecycle,
                            $"GUI supervisor failed-start control-plane request retirement failed. {exception.Message}");
                    }
                }
            }

            state.ScheduleManifestCleanupAfterPublicationTerminates("after failed startup server stop");
            await state.ManifestPublicationFinalization;
            state.LogManifestPublicationFinalizationWarning();

            var ownsCleanup = false;
            lock (SyncRoot)
            {
                if (ReferenceEquals(startingState, state)
                    && state.TryClaimNormalCleanup())
                {
                    ownsCleanup = true;
                    if (!stoppedSafely && !retirementDeferred)
                    {
                        startingState = null;
                        isBootstrapRestartBlocked = true;
                        failedStartResourceRetention = new FailedStartResourceRetention(
                            state.Server,
                            state.ServiceProvider,
                            state.DaemonLogger);
                    }
                }
            }

            if (!ownsCleanup)
            {
                return false;
            }

            if (retirementDeferred)
            {
                state.AttachDeferredResourceRelease(
                    CompleteDeferredFailedStartCleanupAsync(state, retirementTask));
            }
            else if (stoppedSafely)
            {
                var providerDisposedSafely = true;
                state.ReleaseManagedResourcesOnce(() =>
                {
                    providerDisposedSafely = DisposeServiceProvider(
                        state.ServiceProvider,
                        state.DaemonLogger);
                });
                if (providerDisposedSafely)
                {
                    ClearStartingState(state);
                }
                else
                {
                    stoppedSafely = false;
                    lock (SyncRoot)
                    {
                        if (ReferenceEquals(startingState, state))
                        {
                            startingState = null;
                        }

                        isBootstrapRestartBlocked = true;
                        failedStartResourceRetention = new FailedStartResourceRetention(
                            state.Server,
                            state.ServiceProvider,
                            state.DaemonLogger);
                    }
                }
            }

            state.DisposeCancellationSource();
            return stoppedSafely;
        }

        private static async Task<bool> CompleteDeferredFailedStartCleanupAsync (
            StartingGuiSupervisorState state,
            Task retirementTask)
        {
            try
            {
                await retirementTask;
            }
            catch (Exception exception)
            {
                LogWarningBestEffort(
                    state.DaemonLogger,
                    $"Deferred GUI supervisor failed-start request retirement failed. {exception.Message}");
                return false;
            }

            lock (SyncRoot)
            {
                if (!ReferenceEquals(startingState, state) || isEditorLifecycleStopping)
                {
                    return false;
                }
            }

            var providerDisposedSafely = true;
            state.ReleaseManagedResourcesOnce(() =>
            {
                providerDisposedSafely = DisposeServiceProvider(
                    state.ServiceProvider,
                    state.DaemonLogger);
            });
            if (!providerDisposedSafely)
            {
                lock (SyncRoot)
                {
                    isBootstrapRestartBlocked = true;
                }

                return false;
            }

            ClearStartingState(state);
            return true;
        }

        private static async Task RestartAfterDeferredFailedStartCleanupAsync (
            StartingGuiSupervisorState state)
        {
            var releasedSafely = await state.WaitForDeferredResourceReleaseAsync();
            if (releasedSafely)
            {
                RestartScheduler.ScheduleAfterSafeCleanup();
                return;
            }

            lock (SyncRoot)
            {
                if (isEditorLifecycleStopping)
                {
                    return;
                }
            }

            BlockRestartWhenUnsafe(stoppedSafely: false);
        }

        private static void ReleaseUntrackedStartingResources (
            IUnityIpcServer server,
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger)
        {
            ReleaseStartingServerForEditorLifecycleEvent(server, daemonLogger);

            if (server == null)
            {
                _ = DisposeServiceProvider(serviceProvider, daemonLogger);
            }
        }

        internal static void ReleaseUnattachedStartingResources (
            IUnityIpcServer server,
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger)
        {
            ReleaseStartingServerForEditorLifecycleEvent(server, daemonLogger);
            _ = DisposeServiceProvider(serviceProvider, daemonLogger);
        }

        private static void ReleaseStartingServerForEditorLifecycleEvent (
            IUnityIpcServer server,
            IDaemonLogger daemonLogger)
        {
            if (server == null)
            {
                return;
            }

            try
            {
                server.ReleaseForEditorLifecycleEvent();
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI supervisor startup server lifecycle release failed. {exception.Message}");
            }
        }

        private static async Task MonitorAsync (ActiveGuiSupervisorState state)
        {
            try
            {
                try
                {
                    await state.Server.WaitForTerminationAsync(CancellationToken.None);
                    state.DaemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        "GUI supervisor IPC server loop terminated.");
                }
                finally
                {
                    // A faulted supervisor listener is terminal as well. Clear the published owner
                    // and manifest so a later bootstrap can create a fresh endpoint generation.
                    var stoppedSafely = await StopStateAsync(state);
                    if (stoppedSafely)
                    {
                        RestartScheduler.ScheduleAfterSafeCleanup();
                    }
                    else if (state.HasDeferredResourceRelease)
                    {
                        var releasedSafely = await state.WaitForDeferredResourceReleaseAsync();
                        BlockRestartWhenUnsafe(releasedSafely);
                        if (releasedSafely)
                        {
                            RestartScheduler.ScheduleAfterSafeCleanup();
                        }
                    }
                    else
                    {
                        BlockRestartWhenUnsafe(stoppedSafely: false);
                    }
                }
            }
            catch (Exception exception)
            {
                state.DaemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "GUI supervisor monitor failed.",
                    exception);
            }
        }

        private static void MarkStoppingState (ActiveGuiSupervisorState state)
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(activeState, state))
                {
                    activeState = null;
                }

                stoppingState = state;
            }
        }

        private static void ClearStoppingState (ActiveGuiSupervisorState state)
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(stoppingState, state))
                {
                    stoppingState = null;
                }
            }
        }

        internal static async Task<bool> StopStateAsync (ActiveGuiSupervisorState state)
        {
            if (!state.TryBeginStop())
            {
                return await state.WaitForStopCompletionAsync();
            }

            RestartScheduler.EndGenerationStabilityObservation(state);
            MarkStoppingState(state);

            var stoppedSafely = true;
            var retirementDeferred = false;
            Task retirementTask = null;
            try
            {
                await state.Server.StopAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                stoppedSafely = false;
                state.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI supervisor IPC server stop failed. {exception.Message}");
            }

            if (stoppedSafely)
            {
                try
                {
                    retirementTask = state.ControlPlaneRequestLifetime.WaitForRetirementAsync();
                    stoppedSafely = await UnityHostGenerationRetirementPolicy
                        .WaitWithinForegroundDeadlineAsync(retirementTask);
                    if (!stoppedSafely)
                    {
                        retirementDeferred = true;
                        state.DaemonLogger.Warning(
                            DaemonLogCategories.Lifecycle,
                            $"GUI supervisor control-plane request retirement exceeded its {UnityHostGenerationRetirementPolicy.ForegroundDeadline.TotalMilliseconds:0}ms foreground deadline.");
                    }
                }
                catch (Exception exception)
                {
                    stoppedSafely = false;
                    state.DaemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        $"GUI supervisor control-plane request retirement failed. {exception.Message}");
                }
            }

            if (retirementDeferred)
            {
                state.AttachDeferredResourceRelease(
                    CompleteDeferredStateCleanupAsync(state, retirementTask));
            }
            else if (stoppedSafely)
            {
                stoppedSafely = ReleaseStateResources(state);
                if (stoppedSafely)
                {
                    ClearStoppingState(state);
                }
            }

            if (!stoppedSafely)
            {
                DeleteStateManifest(state);
                EnsureEditorLifecycleSubscriptions();
            }

            state.CompleteStop(stoppedSafely);
            return await state.WaitForStopCompletionAsync();
        }

        private static async Task<bool> CompleteDeferredStateCleanupAsync (
            ActiveGuiSupervisorState state,
            Task retirementTask)
        {
            try
            {
                await retirementTask;
            }
            catch (Exception exception)
            {
                LogWarningBestEffort(
                    state.DaemonLogger,
                    $"Deferred GUI supervisor control-plane request retirement failed. {exception.Message}");
                return false;
            }

            lock (SyncRoot)
            {
                if (!ReferenceEquals(stoppingState, state) || isEditorLifecycleStopping)
                {
                    return false;
                }
            }

            var releasedSafely = ReleaseStateResources(state);
            if (releasedSafely)
            {
                ClearStoppingState(state);
            }

            return releasedSafely;
        }

        private static void BlockRestartWhenUnsafe (bool stoppedSafely)
        {
            if (stoppedSafely)
            {
                return;
            }

            lock (SyncRoot)
            {
                isBootstrapRestartBlocked = true;
            }

            RestartScheduler.CancelPendingRestart();
        }

        private static void ReleaseStateForEditorLifecycleEvent (ActiveGuiSupervisorState state)
        {
            _ = state.TryBeginStop();
            RestartScheduler.EndGenerationStabilityObservation(state);
            if (!state.TryBeginResourceRelease())
            {
                return;
            }

            var releasedSafely = true;
            try
            {
                state.Server.ReleaseForEditorLifecycleEvent();
            }
            catch (Exception exception)
            {
                releasedSafely = false;
                state.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI supervisor IPC server lifecycle release failed. {exception.Message}");
            }

            DeleteStateManifest(state);
            // Accepted request work can still be unwinding because lifecycle release is synchronous.
            // Keep the provider alive until process exit or AppDomain unload.
            state.CompleteStop(releasedSafely);
        }

        private static bool ReleaseStateResources (ActiveGuiSupervisorState state)
        {
            if (!state.TryBeginResourceRelease())
            {
                return false;
            }

            DeleteStateManifest(state);
            return DisposeServiceProvider(state.ServiceProvider, state.DaemonLogger);
        }

        private static void DeleteStateManifest (ActiveGuiSupervisorState state)
        {
            try
            {
                UnityGuiSupervisorPersistence.Delete(
                    state.StorageRoot,
                    state.ProjectFingerprint,
                    state.SessionToken);
            }
            catch (Exception exception)
            {
                state.DaemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI supervisor manifest cleanup failed. {exception.Message}");
            }
        }

        private static bool DisposeServiceProvider (
            IServiceProvider serviceProvider,
            IDaemonLogger daemonLogger)
        {
            if (serviceProvider is not IDisposable disposable)
            {
                return true;
            }

            try
            {
                disposable.Dispose();
                return true;
            }
            catch (Exception exception)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"GUI supervisor service provider disposal failed. {exception.Message}");
                return false;
            }
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
                // Diagnostics must not turn a completed publication or cleanup into a lifecycle failure.
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
            AssemblyReloadEvents.beforeAssemblyReload -= StopSynchronously;
            EditorApplication.quitting -= StopSynchronously;
            AssemblyReloadEvents.beforeAssemblyReload += StopSynchronously;
            EditorApplication.quitting += StopSynchronously;
        }

        private static void StopSynchronously ()
        {
            ActiveGuiSupervisorState capturedState;
            StartingGuiSupervisorState capturedStartingState;
            FailedStartResourceRetention capturedFailedStartResources;
            lock (SyncRoot)
            {
                isEditorLifecycleStopping = true;
                capturedState = activeState ?? stoppingState;
                capturedStartingState = ClaimStartingStateForEditorLifecycleEventWithoutLock();
                capturedFailedStartResources = failedStartResourceRetention;
                activeState = null;
                stoppingState = null;
                failedStartResourceRetention = null;
            }

            RestartScheduler.StopForEditorLifecycleEvent();
            ReleaseStartingStateForEditorLifecycleEvent(capturedStartingState);

            if (capturedState != null)
            {
                ReleaseStateForEditorLifecycleEvent(capturedState);
            }

            if (capturedFailedStartResources != null)
            {
                if (capturedFailedStartResources.Server != null)
                {
                    try
                    {
                        capturedFailedStartResources.Server.ReleaseForEditorLifecycleEvent();
                    }
                    catch (Exception exception)
                    {
                        capturedFailedStartResources.DaemonLogger.Warning(
                            DaemonLogCategories.Lifecycle,
                            $"GUI supervisor failed-start server lifecycle release failed. {exception.Message}");
                    }
                }

                // The failed generation can still own accepted work. The old AppDomain or process owns cleanup.
            }
        }

        private static bool CanScheduleRestart ()
        {
            lock (SyncRoot)
            {
                return !isBootstrapRestartBlocked
                    && !isEditorLifecycleStopping
                    && activeState == null
                    && startingState == null
                    && stoppingState == null;
            }
        }

        private static void StartScheduledRestart ()
        {
            ObserveFault(StartAsync());
        }

        private static void ObserveFault (Task task)
        {
            _ = task.ContinueWith(
                static completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private static StartingGuiSupervisorState ClaimStartingStateForEditorLifecycleEventWithoutLock ()
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
            StartingGuiSupervisorState state)
        {
            if (state == null || !state.CanReleaseForEditorLifecycleEvent)
            {
                return;
            }

            state.Cancel();
            state.ReleaseManagedResourcesOnce(() =>
                ReleaseUntrackedStartingResources(
                    state.Server,
                    state.ServiceProvider,
                    state.DaemonLogger));
            state.ScheduleManifestCleanupAfterPublicationTerminates(
                "during editor lifecycle event");
            state.DisposeCancellationSource();
        }

        private static void ClearStartingState (StartingGuiSupervisorState state)
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(startingState, state))
                {
                    startingState = null;
                }
            }
        }

        internal sealed class StartingGuiSupervisorState
        {
            private const int CompletionOwnerActiveState = 1;

            private const int CompletionOwnerEditorLifecycle = 2;

            private const int CompletionOwnerNormalCleanup = 3;

            private readonly CancellationTokenSource cancellationSource = new CancellationTokenSource();

            private readonly CancellationToken cancellationToken;

            private readonly object managedResourceReleaseSyncRoot = new object();

            private readonly object manifestPublicationWarningSyncRoot = new object();

            private readonly TaskCompletionSource<bool> manifestPublicationFinalizationCompletionSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int completionOwner;

            private int cancellationSourceDisposed;

            private int manifestPublicationFinalizationStarted;

            private bool managedResourcesReleased;

            private UnityGuiSupervisorPersistence.PublicationLease publicationLease;

            private Task<GuiSupervisorManifestJsonContract> manifestPublicationTask;

            private Task<bool> deferredResourceReleaseTask;

            private string manifestPublicationFinalizationWarning;

            public StartingGuiSupervisorState (IDaemonLogger daemonLogger)
            {
                DaemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
                cancellationToken = cancellationSource.Token;
            }

            public CancellationToken CancellationToken => cancellationToken;

            public IDaemonLogger DaemonLogger { get; }

            public string StorageRoot { get; private set; }

            public ProjectFingerprint ProjectFingerprint { get; private set; }

            public IpcSessionToken SessionToken { get; private set; }

            public IUnityIpcServer Server { get; private set; }

            public IServiceProvider ServiceProvider { get; private set; }

            public IUnityControlPlaneRequestLifetime ControlPlaneRequestLifetime { get; private set; }

            public Task ManifestPublicationFinalization =>
                manifestPublicationFinalizationCompletionSource.Task;

            public bool IsUnclaimed => Volatile.Read(ref completionOwner) == 0;

            public bool IsNormalCleanupClaimed =>
                Volatile.Read(ref completionOwner) == CompletionOwnerNormalCleanup;

            public bool HasDeferredResourceRelease =>
                Volatile.Read(ref deferredResourceReleaseTask) != null;

            public bool CanReleaseForEditorLifecycleEvent
            {
                get
                {
                    var owner = Volatile.Read(ref completionOwner);
                    return owner == CompletionOwnerEditorLifecycle
                        || owner == CompletionOwnerNormalCleanup;
                }
            }

            public void AttachIdentity (
                string storageRoot,
                ProjectFingerprint projectFingerprint,
                IpcSessionToken sessionToken)
            {
                if (!IsUnclaimed)
                {
                    throw new InvalidOperationException("The GUI supervisor startup generation no longer owns resources.");
                }

                if (string.IsNullOrWhiteSpace(storageRoot))
                {
                    throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
                }

                if (projectFingerprint == null)
                {
                    throw new ArgumentNullException(nameof(projectFingerprint));
                }

                if (sessionToken == null)
                {
                    throw new ArgumentNullException(nameof(sessionToken));
                }

                StorageRoot = storageRoot;
                ProjectFingerprint = projectFingerprint;
                SessionToken = sessionToken;
            }

            public void AttachResources (
                IUnityIpcServer server,
                IServiceProvider serviceProvider,
                IUnityControlPlaneRequestLifetime controlPlaneRequestLifetime)
            {
                if (!IsUnclaimed)
                {
                    throw new InvalidOperationException("The GUI supervisor startup generation no longer owns resources.");
                }

                Server = server;
                ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                ControlPlaneRequestLifetime = controlPlaneRequestLifetime
                    ?? throw new ArgumentNullException(nameof(controlPlaneRequestLifetime));
            }

            public void AttachDeferredResourceRelease (Task<bool> resourceReleaseTask)
            {
                if (resourceReleaseTask == null)
                {
                    throw new ArgumentNullException(nameof(resourceReleaseTask));
                }

                if (Interlocked.CompareExchange(
                        ref deferredResourceReleaseTask,
                        resourceReleaseTask,
                        null) != null)
                {
                    throw new InvalidOperationException(
                        "GUI supervisor startup generation already owns a deferred resource release.");
                }
            }

            public Task<bool> WaitForDeferredResourceReleaseAsync ()
            {
                return Volatile.Read(ref deferredResourceReleaseTask)
                    ?? throw new InvalidOperationException(
                        "GUI supervisor startup generation has no deferred resource release.");
            }

            public void ValidateManifest (GuiSupervisorManifestJsonContract manifest)
            {
                if (!IsUnclaimed)
                {
                    throw new InvalidOperationException("The GUI supervisor startup generation no longer owns resources.");
                }

                if (manifest == null)
                {
                    throw new ArgumentNullException(nameof(manifest));
                }

                if (SessionToken == null || SessionToken != manifest.SessionToken)
                {
                    throw new InvalidOperationException(
                        "The GUI supervisor manifest does not belong to the startup generation.");
                }

            }

            public void AttachPublicationLease (
                UnityGuiSupervisorPersistence.PublicationLease publicationLease)
            {
                if (!IsUnclaimed)
                {
                    throw new InvalidOperationException("The GUI supervisor startup generation no longer owns resources.");
                }

                if (this.publicationLease != null)
                {
                    throw new InvalidOperationException("The GUI supervisor publication lease is already attached.");
                }

                this.publicationLease = publicationLease
                    ?? throw new ArgumentNullException(nameof(publicationLease));
            }

            public void AttachManifestPublicationTask (
                Task<GuiSupervisorManifestJsonContract> publicationTask)
            {
                if (!IsUnclaimed)
                {
                    throw new InvalidOperationException(
                        "The GUI supervisor startup generation no longer owns publication.");
                }

                if (publicationLease == null)
                {
                    throw new InvalidOperationException(
                        "A GUI supervisor publication lease is required before publication starts.");
                }

                if (manifestPublicationTask != null)
                {
                    throw new InvalidOperationException(
                        "GUI supervisor manifest publication has already started.");
                }

                manifestPublicationTask = publicationTask
                    ?? throw new ArgumentNullException(nameof(publicationTask));
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

                    managedResourcesReleased = true;
                    releaseResources();
                }
            }

            public void Cancel ()
            {
                try
                {
                    cancellationSource.Cancel();
                }
                catch (Exception exception)
                {
                    DaemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        $"GUI supervisor startup cancellation callback failed. {exception.Message}");
                }
            }

            public void EnsureManifestPublicationReadyForCommit ()
            {
                if (manifestPublicationTask == null
                    || !manifestPublicationTask.IsCompleted
                    || manifestPublicationTask.IsCanceled
                    || manifestPublicationTask.IsFaulted)
                {
                    throw new InvalidOperationException(
                        "GUI supervisor manifest publication must complete successfully before active ownership commits.");
                }

                if (publicationLease == null
                    || Volatile.Read(ref manifestPublicationFinalizationStarted) != 0)
                {
                    throw new InvalidOperationException(
                        "GUI supervisor publication ownership was finalized before active ownership committed.");
                }
            }

            public void ReleasePublicationLeaseAfterSuccessfulPublication ()
            {
                if (Interlocked.Exchange(ref manifestPublicationFinalizationStarted, 1) != 0)
                {
                    LogWarningBestEffort(
                        DaemonLogger,
                        "GUI supervisor publication lease was already finalized after active publication.");
                    return;
                }

                var ownedPublicationLease = Interlocked.Exchange(ref publicationLease, null);
                try
                {
                    ownedPublicationLease?.Dispose();
                }
                catch (Exception exception)
                {
                    LogWarningBestEffort(
                        DaemonLogger,
                        $"GUI supervisor publication lease release failed after successful publication. {exception.Message}");
                }
                finally
                {
                    manifestPublicationFinalizationCompletionSource.TrySetResult(true);
                }
            }

            public void ScheduleManifestCleanupAfterPublicationTerminates (string cleanupContext)
            {
                if (Interlocked.Exchange(ref manifestPublicationFinalizationStarted, 1) != 0)
                {
                    return;
                }

                var publicationTask = manifestPublicationTask;
                if (publicationTask == null || publicationTask.IsCompleted)
                {
                    FinalizeFailedManifestPublication(cleanupContext, publicationTask);
                    return;
                }

                _ = publicationTask.ContinueWith(
                    completedTask => FinalizeFailedManifestPublication(cleanupContext, completedTask),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            public void LogManifestPublicationFinalizationWarning ()
            {
                string warning;
                lock (manifestPublicationWarningSyncRoot)
                {
                    warning = manifestPublicationFinalizationWarning;
                    manifestPublicationFinalizationWarning = null;
                }

                if (!string.IsNullOrEmpty(warning))
                {
                    LogWarningBestEffort(DaemonLogger, warning);
                }
            }

            public void DisposeCancellationSource ()
            {
                if (Interlocked.Exchange(ref cancellationSourceDisposed, 1) != 0)
                {
                    return;
                }

                cancellationSource.Dispose();
            }

            private bool TryClaimCompletion (int owner)
            {
                return Interlocked.CompareExchange(ref completionOwner, owner, 0) == 0;
            }

            private void FinalizeFailedManifestPublication (
                string cleanupContext,
                Task publicationTask)
            {
                _ = publicationTask?.Exception;
                var ownedPublicationLease = Interlocked.Exchange(ref publicationLease, null);
                try
                {
                    if (ownedPublicationLease != null
                        && SessionToken != null)
                    {
                        ownedPublicationLease.DeleteIfOwned(SessionToken);
                    }
                }
                catch (Exception exception)
                {
                    RecordManifestPublicationFinalizationWarning(
                        $"GUI supervisor manifest cleanup under publication lease {cleanupContext} failed. {exception.Message}");
                }
                finally
                {
                    try
                    {
                        ownedPublicationLease?.Dispose();
                    }
                    catch (Exception exception)
                    {
                        RecordManifestPublicationFinalizationWarning(
                            $"GUI supervisor publication lease release {cleanupContext} failed. {exception.Message}");
                    }

                    manifestPublicationFinalizationCompletionSource.TrySetResult(true);
                }
            }

            private void RecordManifestPublicationFinalizationWarning (string warning)
            {
                lock (manifestPublicationWarningSyncRoot)
                {
                    manifestPublicationFinalizationWarning = string.IsNullOrEmpty(manifestPublicationFinalizationWarning)
                        ? warning
                        : $"{manifestPublicationFinalizationWarning} {warning}";
                }
            }
        }

        private sealed class FailedStartResourceRetention
        {
            public FailedStartResourceRetention (
                IUnityIpcServer server,
                IServiceProvider serviceProvider,
                IDaemonLogger daemonLogger)
            {
                Server = server ?? throw new ArgumentNullException(nameof(server));
                ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                DaemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
            }

            public IUnityIpcServer Server { get; }

            public IServiceProvider ServiceProvider { get; }

            public IDaemonLogger DaemonLogger { get; }
        }

        internal sealed class ActiveGuiSupervisorState
        {
            private int stopStarted;

            private int stopSafety;

            private int resourcesReleased;

            private Task<bool> deferredResourceReleaseTask;

            private readonly TaskCompletionSource<bool> stopCompletionSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public ActiveGuiSupervisorState (
                IpcSessionToken sessionToken,
                IUnityIpcServer server,
                IServiceProvider serviceProvider,
                IUnityControlPlaneRequestLifetime controlPlaneRequestLifetime,
                IDaemonLogger daemonLogger,
                string storageRoot,
                ProjectFingerprint projectFingerprint)
            {
                SessionToken = sessionToken ?? throw new ArgumentNullException(nameof(sessionToken));
                Server = server ?? throw new ArgumentNullException(nameof(server));
                ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
                ControlPlaneRequestLifetime = controlPlaneRequestLifetime
                    ?? throw new ArgumentNullException(nameof(controlPlaneRequestLifetime));
                DaemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
                StorageRoot = storageRoot ?? throw new ArgumentNullException(nameof(storageRoot));
                ProjectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));
            }

            public IpcSessionToken SessionToken { get; }

            public IUnityIpcServer Server { get; }

            public IServiceProvider ServiceProvider { get; }

            public IUnityControlPlaneRequestLifetime ControlPlaneRequestLifetime { get; }

            public IDaemonLogger DaemonLogger { get; }

            public string StorageRoot { get; }

            public ProjectFingerprint ProjectFingerprint { get; }

            public bool HasDeferredResourceRelease =>
                Volatile.Read(ref deferredResourceReleaseTask) != null;

            public bool TryBeginStop ()
            {
                return Interlocked.Exchange(ref stopStarted, 1) == 0;
            }

            public void CompleteStop (bool stoppedSafely)
            {
                var stopSafetyValue = stoppedSafely ? 1 : 2;
                if (Interlocked.CompareExchange(ref stopSafety, stopSafetyValue, 0) == 0)
                {
                    stopCompletionSource.TrySetResult(stoppedSafely);
                }
            }

            public Task<bool> WaitForStopCompletionAsync ()
            {
                return stopCompletionSource.Task;
            }

            public void AttachDeferredResourceRelease (Task<bool> resourceReleaseTask)
            {
                if (resourceReleaseTask == null)
                {
                    throw new ArgumentNullException(nameof(resourceReleaseTask));
                }

                if (Interlocked.CompareExchange(
                        ref deferredResourceReleaseTask,
                        resourceReleaseTask,
                        null) != null)
                {
                    throw new InvalidOperationException(
                        "GUI supervisor generation already owns a deferred resource release.");
                }
            }

            public Task<bool> WaitForDeferredResourceReleaseAsync ()
            {
                return Volatile.Read(ref deferredResourceReleaseTask)
                    ?? throw new InvalidOperationException(
                        "GUI supervisor generation has no deferred resource release.");
            }

            public bool TryBeginResourceRelease ()
            {
                return Interlocked.Exchange(ref resourcesReleased, 1) == 0;
            }
        }
    }

    /// <summary> Schedules safe GUI supervisor restarts on the Unity editor update loop with capped backoff. </summary>
    internal sealed class UnityGuiSupervisorRestartScheduler
    {
        internal static readonly TimeSpan InitialRestartDelay = TimeSpan.FromMilliseconds(100);

        internal static readonly TimeSpan MaximumRestartDelay = TimeSpan.FromSeconds(5);

        internal static readonly TimeSpan StableGenerationWindow = TimeSpan.FromSeconds(5);

        private readonly object syncRoot = new object();

        private readonly Func<double> editorTimeProvider;

        private readonly Action<EditorApplication.CallbackFunction> editorUpdateSubscriber;

        private readonly Action<EditorApplication.CallbackFunction> editorUpdateUnsubscriber;

        private readonly Func<bool> canRestart;

        private readonly Action restart;

        private readonly EditorApplication.CallbackFunction restartUpdateCallback;

        private int consecutiveFailureCount;

        private double restartAtEditorTime;

        private bool isRestartScheduled;

        private object stabilityGeneration;

        private double generationStartedAtEditorTime;

        private bool isEditorLifecycleStopped;

        /// <summary> Initializes one scheduler with its Unity update-loop boundary and restart admission policy. </summary>
        internal UnityGuiSupervisorRestartScheduler (
            Func<double> editorTimeProvider,
            Action<EditorApplication.CallbackFunction> editorUpdateSubscriber,
            Action<EditorApplication.CallbackFunction> editorUpdateUnsubscriber,
            Func<bool> canRestart,
            Action restart)
        {
            this.editorTimeProvider = editorTimeProvider ?? throw new ArgumentNullException(nameof(editorTimeProvider));
            this.editorUpdateSubscriber = editorUpdateSubscriber ?? throw new ArgumentNullException(nameof(editorUpdateSubscriber));
            this.editorUpdateUnsubscriber = editorUpdateUnsubscriber ?? throw new ArgumentNullException(nameof(editorUpdateUnsubscriber));
            this.canRestart = canRestart ?? throw new ArgumentNullException(nameof(canRestart));
            this.restart = restart ?? throw new ArgumentNullException(nameof(restart));
            restartUpdateCallback = OnRestartEditorUpdate;
        }

        /// <summary> Schedules a successor only after the failed generation released its resources safely. </summary>
        internal void ScheduleAfterSafeCleanup ()
        {
            if (!canRestart())
            {
                return;
            }

            lock (syncRoot)
            {
                if (isEditorLifecycleStopped || isRestartScheduled)
                {
                    return;
                }

                var editorTime = GetFiniteEditorTime();
                restartAtEditorTime = editorTime + ResolveRestartDelay(consecutiveFailureCount).TotalSeconds;
                editorUpdateSubscriber(restartUpdateCallback);
                isRestartScheduled = true;
                if (consecutiveFailureCount < int.MaxValue)
                {
                    consecutiveFailureCount++;
                }
            }
        }

        /// <summary> Cancels a pending retry when another startup generation owns admission. </summary>
        internal void CancelPendingRestart ()
        {
            lock (syncRoot)
            {
                CancelPendingRestartWithoutLock();
            }
        }

        /// <summary> Starts measuring whether one published generation remains active long enough to be stable. </summary>
        internal void BeginGenerationStabilityObservation (object generation)
        {
            if (generation == null)
            {
                throw new ArgumentNullException(nameof(generation));
            }

            lock (syncRoot)
            {
                if (isEditorLifecycleStopped)
                {
                    return;
                }

                var editorTime = GetFiniteEditorTime();
                CancelPendingRestartWithoutLock();
                CancelGenerationStabilityObservationWithoutLock();
                stabilityGeneration = generation;
                generationStartedAtEditorTime = editorTime;
            }
        }

        /// <summary> Cancels stability measurement when the observed generation stops or loses ownership. </summary>
        internal void EndGenerationStabilityObservation (object generation)
        {
            if (generation == null)
            {
                throw new ArgumentNullException(nameof(generation));
            }

            lock (syncRoot)
            {
                if (!ReferenceEquals(stabilityGeneration, generation))
                {
                    return;
                }

                var editorTime = GetFiniteEditorTime();
                var stableAtEditorTime = generationStartedAtEditorTime + StableGenerationWindow.TotalSeconds;
                CancelGenerationStabilityObservationWithoutLock();
                if (editorTime >= stableAtEditorTime)
                {
                    consecutiveFailureCount = 0;
                }
            }
        }

        /// <summary> Permanently disables retry scheduling for the current AppDomain lifecycle. </summary>
        internal void StopForEditorLifecycleEvent ()
        {
            lock (syncRoot)
            {
                isEditorLifecycleStopped = true;
                try
                {
                    CancelPendingRestartWithoutLock();
                }
                finally
                {
                    CancelGenerationStabilityObservationWithoutLock();
                }
            }
        }

        internal static TimeSpan ResolveRestartDelay (int consecutiveFailureCount)
        {
            if (consecutiveFailureCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(consecutiveFailureCount));
            }

            var delayMilliseconds = InitialRestartDelay.TotalMilliseconds;
            var maximumDelayMilliseconds = MaximumRestartDelay.TotalMilliseconds;
            for (var attempt = 0;
                attempt < consecutiveFailureCount && delayMilliseconds < maximumDelayMilliseconds;
                attempt++)
            {
                delayMilliseconds = Math.Min(delayMilliseconds * 2d, maximumDelayMilliseconds);
            }

            return TimeSpan.FromMilliseconds(delayMilliseconds);
        }

        private void OnRestartEditorUpdate ()
        {
            var restartIsDue = false;
            lock (syncRoot)
            {
                if (!isRestartScheduled || isEditorLifecycleStopped)
                {
                    return;
                }

                var editorTime = GetFiniteEditorTime();
                if (editorTime < restartAtEditorTime)
                {
                    return;
                }

                CancelPendingRestartWithoutLock();
                restartIsDue = true;
            }

            if (restartIsDue && canRestart())
            {
                restart();
            }
        }

        private void CancelPendingRestartWithoutLock ()
        {
            if (!isRestartScheduled)
            {
                return;
            }

            try
            {
                editorUpdateUnsubscriber(restartUpdateCallback);
            }
            finally
            {
                isRestartScheduled = false;
            }
        }

        private void CancelGenerationStabilityObservationWithoutLock ()
        {
            stabilityGeneration = null;
            generationStartedAtEditorTime = 0d;
        }

        private double GetFiniteEditorTime ()
        {
            var editorTime = editorTimeProvider();
            if (double.IsNaN(editorTime) || double.IsInfinity(editorTime))
            {
                throw new InvalidOperationException("Unity editor time must be finite when scheduling GUI supervisor lifecycle work.");
            }

            return editorTime;
        }
    }
}
