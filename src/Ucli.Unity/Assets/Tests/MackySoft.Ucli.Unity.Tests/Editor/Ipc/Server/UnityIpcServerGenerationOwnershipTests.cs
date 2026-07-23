using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcServerGenerationOwnershipTests
    {
        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenReleasedGenerationObservesStartupCancellationAfterRestart_DoesNotStopRestartedGeneration () => UniTask.ToCoroutine(async () =>
        {
            var listener = new DelayedStartupCancellationTransportListener();
            var server = CreateServer(listener);
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-start-cancellation");
            var firstStartTask = server.StartAsync(endpoint);

            try
            {
                await TestAwaiter.WaitAsync(
                    listener.FirstRunEntered,
                    "First listener generation entry",
                    SignalWaitTimeout);
                server.ReleaseForEditorLifecycleEvent();

                await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "Restarted listener generation startup",
                    SignalWaitTimeout);
                Assert.That(server.IsRunning, Is.True);

                listener.AllowFirstGenerationCancellation();
                OperationCanceledException firstStartException = null;
                try
                {
                    await TestAwaiter.WaitAsync(
                        firstStartTask,
                        "Released generation startup cancellation",
                        SignalWaitTimeout);
                }
                catch (OperationCanceledException exception)
                {
                    firstStartException = exception;
                }

                Assert.That(firstStartException, Is.Not.Null);
                Assert.That(server.IsRunning, Is.True);
                Assert.That(listener.RestartedGenerationCancellationObserved.IsCompleted, Is.False);
            }
            finally
            {
                listener.AllowFirstGenerationCancellation();
                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Restarted listener generation cleanup",
                    SignalWaitTimeout);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenReleasedGenerationReportsDelayedStartup_DoesNotCompleteReleasedStart () => UniTask.ToCoroutine(async () =>
        {
            var listener = new DelayedReleasedGenerationStartupTransportListener();
            var server = CreateServer(listener);
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-delayed-startup");
            var releasedStartTask = server.StartAsync(endpoint);

            try
            {
                await TestAwaiter.WaitAsync(
                    listener.FirstRunEntered,
                    "Released listener generation entry",
                    SignalWaitTimeout);
                server.ReleaseForEditorLifecycleEvent();
                await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "Successor listener generation startup",
                    SignalWaitTimeout);

                listener.ReportReleasedGenerationStartup();
                OperationCanceledException releasedStartException = null;
                try
                {
                    await TestAwaiter.WaitAsync(
                        releasedStartTask,
                        "Released listener generation delayed startup",
                        SignalWaitTimeout);
                }
                catch (OperationCanceledException exception)
                {
                    releasedStartException = exception;
                }

                Assert.That(releasedStartException, Is.Not.Null);
                Assert.That(server.IsRunning, Is.True);
                Assert.That(listener.SuccessorCancellationObserved.IsCompleted, Is.False);
            }
            finally
            {
                listener.ReportReleasedGenerationStartup();
                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Delayed startup successor cleanup",
                    SignalWaitTimeout);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenLifecycleReleaseOwnsCancellation_DoesNotDisposeSourceFromReleasedStartCleanup () => UniTask.ToCoroutine(async () =>
        {
            var listener = new StartupReleaseCancellationOwnershipTransportListener();
            var server = CreateServer(listener);
            listener.ReleaseAfterStartup = server.ReleaseForEditorLifecycleEvent;
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-startup-cancellation-ownership");
            var releasedStartTask = server.StartAsync(endpoint);

            try
            {
                await TestAwaiter.WaitAsync(
                    listener.CancellationCallbackEntered,
                    "Lifecycle-owned cancellation callback entry",
                    SignalWaitTimeout);
                await TestAwaiter.WaitAsync(
                    listener.ListenerExited,
                    "Released startup listener exit",
                    SignalWaitTimeout);

                OperationCanceledException releasedStartException = null;
                try
                {
                    await TestAwaiter.WaitAsync(
                        releasedStartTask,
                        "Released startup result while lifecycle cancellation remains active",
                        SignalWaitTimeout);
                }
                catch (OperationCanceledException exception)
                {
                    releasedStartException = exception;
                }

                listener.AllowCancellationCallbackCompletion();
                await TestAwaiter.WaitAsync(
                    listener.CancellationCallbackCompleted,
                    "Lifecycle-owned cancellation callback completion",
                    SignalWaitTimeout);

                Assert.That(releasedStartException, Is.Not.Null);
                Assert.That(listener.SourceDisposedDuringCancellationCallback, Is.False);
            }
            finally
            {
                listener.AllowCancellationCallbackCompletion();
                await TestAwaiter.WaitAsync(
                    listener.CancellationCallbackCompleted,
                    "Lifecycle-owned cancellation callback cleanup",
                    SignalWaitTimeout);
                try
                {
                    await TestAwaiter.WaitAsync(
                        releasedStartTask,
                        "Released startup task cleanup",
                        SignalWaitTimeout);
                }
                catch (OperationCanceledException)
                {
                }

                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Released startup cancellation ownership cleanup",
                    SignalWaitTimeout);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Run_WhenReleasedGenerationFailsAfterRestart_DoesNotResetRestartedGeneration () => UniTask.ToCoroutine(async () =>
        {
            var listener = new DelayedFaultTransportListener();
            var daemonLogger = new ExceptionObservingDaemonLogger();
            var server = CreateServer(listener, daemonLogger);
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-delayed-fault");

            try
            {
                await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "First listener generation startup",
                    SignalWaitTimeout);
                server.ReleaseForEditorLifecycleEvent();
                await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "Restarted listener generation startup",
                    SignalWaitTimeout);

                listener.FailFirstGeneration();
                await TestAwaiter.WaitAsync(
                    daemonLogger.ExceptionObserved,
                    "Released generation delayed failure",
                    SignalWaitTimeout);

                Assert.That(server.IsRunning, Is.True);
                Assert.That(listener.RestartedGenerationCancellationObserved.IsCompleted, Is.False);
            }
            finally
            {
                listener.FailFirstGeneration();
                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Restarted listener generation cleanup",
                    SignalWaitTimeout);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenFailedGenerationCleanupRacesWithRestart_DoesNotReleaseRestartedTransport () => UniTask.ToCoroutine(async () =>
        {
            var listener = new BlockingFailedStartCleanupTransportListener();
            var server = CreateServer(listener);
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-cleanup-release");
            using var firstStartCancellationTokenSource = new CancellationTokenSource();
            var firstStartTask = Task.Run(() => server.StartAsync(
                endpoint,
                firstStartCancellationTokenSource.Token));

            try
            {
                await TestAwaiter.WaitAsync(
                    listener.FirstRunEntered,
                    "First failed-start listener generation entry",
                    SignalWaitTimeout);
                firstStartCancellationTokenSource.Cancel();
                await TestAwaiter.WaitAsync(
                    listener.FailedStartCleanupCancellationEntered,
                    "Failed-start cleanup cancellation entry",
                    SignalWaitTimeout);

                var restartedStartTask = server.StartAsync(endpoint);
                var restartedGenerationPublishedDuringCleanup = server.IsRunning;

                listener.AllowFailedStartCleanupCancellation();
                OperationCanceledException firstStartException = null;
                try
                {
                    await TestAwaiter.WaitAsync(
                        firstStartTask,
                        "First failed-start cleanup completion",
                        SignalWaitTimeout);
                }
                catch (OperationCanceledException exception)
                {
                    firstStartException = exception;
                }

                await TestAwaiter.WaitAsync(
                    restartedStartTask,
                    "Restart after failed-start cleanup",
                    SignalWaitTimeout);

                Assert.That(firstStartException, Is.Not.Null);
                Assert.That(restartedGenerationPublishedDuringCleanup, Is.False);
                Assert.That(server.IsRunning, Is.True);
                Assert.That(listener.RestartedGenerationReleaseObserved.IsCompleted, Is.False);
            }
            finally
            {
                listener.AllowFailedStartCleanupCancellation();
                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Failed-start cleanup restart cleanup",
                    SignalWaitTimeout);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Stop_WhenTransportReleaseRacesWithRestart_DoesNotPublishRestartUntilReleaseCompletes () => UniTask.ToCoroutine(async () =>
        {
            var listener = new BlockingStopReleaseTransportListener();
            var server = CreateServer(listener);
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-stop-release");
            Task stopTask = null;

            try
            {
                await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "Listener generation startup before stop race",
                    SignalWaitTimeout);
                stopTask = Task.Run(() => server.StopAsync());
                await TestAwaiter.WaitAsync(
                    listener.FirstReleaseEntered,
                    "First listener generation release entry",
                    SignalWaitTimeout);

                var restartedStartTask = server.StartAsync(endpoint);
                var restartedGenerationPublishedDuringRelease = server.IsRunning;

                listener.AllowFirstRelease();
                await TestAwaiter.WaitAsync(
                    stopTask,
                    "First listener generation stop",
                    SignalWaitTimeout);
                await TestAwaiter.WaitAsync(
                    restartedStartTask,
                    "Restart after first listener generation release",
                    SignalWaitTimeout);

                Assert.That(restartedGenerationPublishedDuringRelease, Is.False);
                Assert.That(server.IsRunning, Is.True);
                Assert.That(listener.RestartedGenerationReleaseObserved.IsCompleted, Is.False);
            }
            finally
            {
                listener.AllowFirstRelease();
                if (stopTask != null)
                {
                    await TestAwaiter.WaitAsync(
                        stopTask,
                        "First listener generation stop cleanup",
                        SignalWaitTimeout);
                }

                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Restarted listener generation cleanup after stop race",
                    SignalWaitTimeout);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReleaseForEditorLifecycleEvent_WhenStopTransitionIsActive_ReleasesTransportSynchronously () => UniTask.ToCoroutine(async () =>
        {
            var listener = new BlockingStopReleaseTransportListener();
            var server = CreateServer(listener);
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-lifecycle-during-stop");
            Task stopTask = null;

            try
            {
                await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "Listener generation startup before lifecycle release race",
                    SignalWaitTimeout);
                stopTask = Task.Run(() => server.StopAsync());
                await TestAwaiter.WaitAsync(
                    listener.FirstReleaseEntered,
                    "Stop transport release entry",
                    SignalWaitTimeout);

                server.ReleaseForEditorLifecycleEvent();

                Assert.That(listener.ReleaseCallCount, Is.EqualTo(2));
            }
            finally
            {
                listener.AllowFirstRelease();
                if (stopTask != null)
                {
                    await TestAwaiter.WaitAsync(
                        stopTask,
                        "Lifecycle release race stop cleanup",
                        SignalWaitTimeout);
                }
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReleaseForEditorLifecycleEvent_WhenStopCompletesDuringLifecycleRelease_BlocksSuccessorUntilReleaseCompletes () => UniTask.ToCoroutine(async () =>
        {
            var listener = new LifecycleReleaseBarrierTransportListener();
            var server = CreateServer(listener);
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-lifecycle-release-barrier");
            Task stopTask = null;
            Task lifecycleReleaseTask = null;
            Task successorStartTask = null;

            try
            {
                await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "Listener generation startup before lifecycle release barrier",
                    SignalWaitTimeout);
                stopTask = Task.Run(() => server.StopAsync());
                await TestAwaiter.WaitAsync(
                    listener.StopReleaseEntered,
                    "Stop release entry before lifecycle release barrier",
                    SignalWaitTimeout);

                lifecycleReleaseTask = Task.Run(server.ReleaseForEditorLifecycleEvent);
                await TestAwaiter.WaitAsync(
                    listener.LifecycleReleaseEntered,
                    "Lifecycle release entry before transition completion",
                    SignalWaitTimeout);

                listener.AllowStopRelease();
                await TestAwaiter.WaitAsync(
                    stopTask,
                    "Stop completion while lifecycle release remains active",
                    SignalWaitTimeout);

                successorStartTask = server.StartAsync(endpoint);
                Assert.That(successorStartTask.IsCompleted, Is.False);
                Assert.That(listener.RunCount, Is.EqualTo(1));

                listener.AllowLifecycleRelease();
                await TestAwaiter.WaitAsync(
                    lifecycleReleaseTask,
                    "Lifecycle release barrier completion",
                    SignalWaitTimeout);
                await TestAwaiter.WaitAsync(
                    successorStartTask,
                    "Successor startup after lifecycle release barrier",
                    SignalWaitTimeout);

                Assert.That(server.IsRunning, Is.True);
                Assert.That(listener.RunCount, Is.EqualTo(2));
            }
            finally
            {
                listener.AllowStopRelease();
                listener.AllowLifecycleRelease();
                if (lifecycleReleaseTask != null)
                {
                    await TestAwaiter.WaitAsync(
                        lifecycleReleaseTask,
                        "Lifecycle release barrier task cleanup",
                        SignalWaitTimeout);
                }

                if (stopTask != null)
                {
                    await TestAwaiter.WaitAsync(
                        stopTask,
                        "Lifecycle release barrier stop cleanup",
                        SignalWaitTimeout);
                }

                if (successorStartTask != null)
                {
                    await TestAwaiter.WaitAsync(
                        successorStartTask,
                        "Lifecycle release barrier successor startup cleanup",
                        SignalWaitTimeout);
                }

                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Lifecycle release barrier successor cleanup",
                    SignalWaitTimeout);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenRunningGenerationFaultCleanupRacesWithRestart_DoesNotPublishRestartUntilCleanupCompletes () => UniTask.ToCoroutine(async () =>
        {
            var listener = new DelayedFaultTransportListener();
            var daemonLogger = new BlockingExceptionDaemonLogger();
            var server = CreateServer(listener, daemonLogger);
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-running-fault");
            Task firstTerminationTask = null;

            try
            {
                await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "Listener generation startup before running fault",
                    SignalWaitTimeout);
                firstTerminationTask = server.WaitForTerminationAsync();
                listener.FailFirstGeneration();
                await TestAwaiter.WaitAsync(
                    daemonLogger.ExceptionEntered,
                    "Running listener generation fault entry",
                    SignalWaitTimeout);

                var restartedStartTask = server.StartAsync(endpoint);
                var restartedGenerationPublishedDuringFaultCleanup = server.IsRunning;

                daemonLogger.AllowExceptionCompletion();
                InvalidOperationException firstTerminationException = null;
                try
                {
                    await TestAwaiter.WaitAsync(
                        firstTerminationTask,
                        "Running listener generation fault completion",
                        SignalWaitTimeout);
                }
                catch (InvalidOperationException exception)
                {
                    firstTerminationException = exception;
                }

                await TestAwaiter.WaitAsync(
                    restartedStartTask,
                    "Restart after running listener generation fault",
                    SignalWaitTimeout);

                Assert.That(firstTerminationException, Is.Not.Null);
                Assert.That(restartedGenerationPublishedDuringFaultCleanup, Is.False);
                Assert.That(server.IsRunning, Is.True);
                Assert.That(listener.RestartedGenerationCancellationObserved.IsCompleted, Is.False);
            }
            finally
            {
                daemonLogger.AllowExceptionCompletion();
                listener.FailFirstGeneration();
                if (firstTerminationTask != null)
                {
                    try
                    {
                        await TestAwaiter.WaitAsync(
                            firstTerminationTask,
                            "Running listener generation fault cleanup",
                            SignalWaitTimeout);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Restarted listener generation cleanup after running fault",
                    SignalWaitTimeout);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Run_WhenActiveListenerReturnsAfterStartup_EndsGenerationAndAllowsRestart () => UniTask.ToCoroutine(async () =>
        {
            var listener = new UnexpectedReturnTransportListener();
            var server = CreateServer(listener);
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-unexpected-return");

            try
            {
                using var terminatedGenerationPublicationFence = await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "Listener generation startup before unexpected return",
                    SignalWaitTimeout);
                var firstTerminationTask = server.WaitForTerminationAsync();

                listener.CompleteFirstGeneration();

                InvalidOperationException terminationException = null;
                try
                {
                    await TestAwaiter.WaitAsync(
                        firstTerminationTask,
                        "Unexpected listener generation return",
                        SignalWaitTimeout);
                }
                catch (InvalidOperationException exception)
                {
                    terminationException = exception;
                }

                Assert.That(terminationException, Is.Not.Null);
                Assert.That(server.IsRunning, Is.False);
                var terminatedGenerationCommitted = false;
                Assert.That(
                    terminatedGenerationPublicationFence.TryCommitActiveOwnership(
                        () => terminatedGenerationCommitted = true),
                    Is.False);
                Assert.That(terminatedGenerationCommitted, Is.False);

                using var restartedGenerationPublicationFence = await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "Restart after unexpected listener return",
                    SignalWaitTimeout);
                var restartedGenerationCommitted = false;
                Assert.That(
                    restartedGenerationPublicationFence.TryCommitActiveOwnership(
                        () => restartedGenerationCommitted = true),
                    Is.True);
                Assert.That(server.IsRunning, Is.True);
                Assert.That(restartedGenerationCommitted, Is.True);
                Assert.That(listener.RunCount, Is.EqualTo(2));
            }
            finally
            {
                listener.CompleteFirstGeneration();
                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Unexpected listener return restart cleanup",
                    SignalWaitTimeout);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenListenerReturnsImmediatelyAfterStartup_DoesNotIssueCommittablePublicationFence () =>
            UniTask.ToCoroutine(() => AssertImmediateListenerTerminationCannotCommitAsync(throwAfterStartup: false));

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenListenerThrowsImmediatelyAfterStartup_DoesNotIssueCommittablePublicationFence () =>
            UniTask.ToCoroutine(() => AssertImmediateListenerTerminationCannotCommitAsync(throwAfterStartup: true));

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenLifecycleReleaseOccursBeforeImmediateListenerReturn_CompletesAsCanceled () => UniTask.ToCoroutine(async () =>
        {
            var listener = new ImmediateTerminationTransportListener(throwAfterStartup: false);
            var server = CreateServer(listener);
            listener.AfterStartup = server.ReleaseForEditorLifecycleEvent;
            var endpoint = new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                "ucli-generation-release-before-immediate-return");

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "Immediate listener return after lifecycle release",
                    SignalWaitTimeout);
            }, "Released immediate listener startup result", SignalWaitTimeout);

            Assert.That(server.IsRunning, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenGenerationIsAlreadyRunning_DoesNotIssueASecondPublicationFence () => UniTask.ToCoroutine(async () =>
        {
            var listener = new UnexpectedReturnTransportListener();
            var server = CreateServer(listener);
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-duplicate-start");
            using var publicationFence = await TestAwaiter.WaitAsync(
                server.StartAsync(endpoint),
                "Initial listener generation startup",
                SignalWaitTimeout);
            InvalidOperationException duplicateStartException = null;
            try
            {
                await server.StartAsync(endpoint);
            }
            catch (InvalidOperationException exception)
            {
                duplicateStartException = exception;
            }
            finally
            {
                listener.CompleteFirstGeneration();
                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Duplicate-start listener cleanup",
                    SignalWaitTimeout);
            }

            Assert.That(duplicateStartException, Is.Not.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ConnectionCompleted_WhenReleasedGenerationReportsShutdownAfterRestart_DoesNotSignalSuccessor () => UniTask.ToCoroutine(async () =>
        {
            var listener = new DelayedConnectionCompletionTransportListener();
            var shutdownSignal = new RecordingDaemonShutdownSignal();
            var server = CreateServer(
                listener,
                daemonShutdownSignal: shutdownSignal);
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-stale-completion");

            try
            {
                await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "Listener generation startup before stale connection completion",
                    SignalWaitTimeout);
                server.ReleaseForEditorLifecycleEvent();
                await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint),
                    "Successor listener generation startup before stale connection completion",
                    SignalWaitTimeout);

                listener.ReportFirstGenerationCompletion(CreateSuccessfulShutdownResult());

                Assert.That(shutdownSignal.SignalCount, Is.EqualTo(0));
                Assert.That(server.IsRunning, Is.True);
            }
            finally
            {
                listener.CompleteFirstGeneration();
                await TestAwaiter.WaitAsync(
                    listener.FirstGenerationExited,
                    "Released listener generation completion after stale callback",
                    SignalWaitTimeout);
                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Stale connection completion successor cleanup",
                    SignalWaitTimeout);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ConnectionGroup_WaitForCompletion_WhenHandlerIgnoresRelease_StopsAtDrainDeadline () => UniTask.ToCoroutine(async () =>
        {
            var connectionGroup = new UnityIpcTransportConnectionGroup(
                NoOpDaemonLogger.Instance,
                maximumActiveConnections: 1);
            var transportHandle = new RecordingDisposable();
            var handlerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handlerCompletion = new TaskCompletionSource<UnityIpcConnectionHandleResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.That(
                connectionGroup.TryStart(
                    transportHandle,
                    async () =>
                    {
                        handlerEntered.TrySetResult(true);
                        return await handlerCompletion.Task;
                    },
                    _ => { },
                    CancellationToken.None),
                Is.True);

            await TestAwaiter.WaitAsync(
                handlerEntered.Task,
                "Non-cooperative connection handler entry",
                SignalWaitTimeout);
            connectionGroup.Release();
            TimeoutException timeoutException = null;
            try
            {
                await connectionGroup.WaitForCompletionAsync(TimeSpan.FromMilliseconds(25));
            }
            catch (TimeoutException exception)
            {
                timeoutException = exception;
            }
            finally
            {
                handlerCompletion.TrySetResult(UnityIpcConnectionHandleResult.NoTerminalResponse);
                await TestAwaiter.WaitAsync(
                    connectionGroup.WaitForCompletionAsync(SignalWaitTimeout),
                    "Non-cooperative connection handler cleanup",
                    SignalWaitTimeout);
            }

            Assert.That(timeoutException, Is.Not.Null);
            Assert.That(transportHandle.DisposeCallCount, Is.GreaterThanOrEqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Stop_WhenListenerIgnoresCancellation_TimesOutAndRejectsSuccessorGeneration () => UniTask.ToCoroutine(async () =>
        {
            var listener = new CancellationIgnoringTransportListener();
            var server = new UnityIpcServer(
                new NoOpConnectionHandler(),
                new List<IUnityIpcTransportListener>
                {
                    listener,
                },
                new NoOpDaemonShutdownSignal(),
                NoOpDaemonLogger.Instance,
                TimeSpan.FromMilliseconds(25));
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-stop-deadline");
            await TestAwaiter.WaitAsync(
                server.StartAsync(endpoint),
                "Cancellation-ignoring listener startup",
                SignalWaitTimeout);

            TimeoutException timeoutException = null;
            InvalidOperationException restartException = null;
            try
            {
                await server.StopAsync();
            }
            catch (TimeoutException exception)
            {
                timeoutException = exception;
            }

            try
            {
                await server.StartAsync(endpoint);
            }
            catch (InvalidOperationException exception)
            {
                restartException = exception;
            }
            finally
            {
                listener.Complete();
                await TestAwaiter.WaitAsync(
                    listener.Exited,
                    "Cancellation-ignoring listener cleanup",
                    SignalWaitTimeout);
            }

            Assert.That(timeoutException, Is.Not.Null);
            Assert.That(restartException, Is.Not.Null);
            Assert.That(listener.RunCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Stop_WhenTransportReleaseThrows_StillWaitsForListenerTermination () => UniTask.ToCoroutine(async () =>
        {
            var listener = new CancellationIgnoringTransportListener
            {
                ThrowOnRelease = true,
            };
            var server = new UnityIpcServer(
                new NoOpConnectionHandler(),
                new List<IUnityIpcTransportListener>
                {
                    listener,
                },
                new NoOpDaemonShutdownSignal(),
                NoOpDaemonLogger.Instance,
                TimeSpan.FromMilliseconds(25));
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-release-failure-deadline");
            await TestAwaiter.WaitAsync(
                server.StartAsync(endpoint),
                "Release-failing listener startup",
                SignalWaitTimeout);

            TimeoutException timeoutException = null;
            try
            {
                await server.StopAsync();
            }
            catch (TimeoutException exception)
            {
                timeoutException = exception;
            }
            finally
            {
                listener.Complete();
                await TestAwaiter.WaitAsync(
                    listener.Exited,
                    "Release-failing listener completion",
                    SignalWaitTimeout);
                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Release-failing listener final cleanup",
                    SignalWaitTimeout);
            }

            Assert.That(timeoutException, Is.Not.Null);
            Assert.That(listener.ReleaseCallCount, Is.GreaterThanOrEqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenFailedStartCleanupTimesOut_StopStillReportsUnsafeTermination () => UniTask.ToCoroutine(async () =>
        {
            var listener = new CancellationIgnoringFailedStartTransportListener();
            var server = new UnityIpcServer(
                new NoOpConnectionHandler(),
                new List<IUnityIpcTransportListener>
                {
                    listener,
                },
                new NoOpDaemonShutdownSignal(),
                NoOpDaemonLogger.Instance,
                TimeSpan.FromMilliseconds(25));
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-generation-failed-start-deadline");
            using var startupCancellationTokenSource = new CancellationTokenSource();
            var startTask = server.StartAsync(endpoint, startupCancellationTokenSource.Token);

            await TestAwaiter.WaitAsync(
                listener.RunEntered,
                "Cancellation-ignoring failed-start listener entry",
                SignalWaitTimeout);
            startupCancellationTokenSource.Cancel();
            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await TestAwaiter.WaitAsync(
                    startTask,
                    "Cancellation-ignoring failed-start cleanup",
                    SignalWaitTimeout);
            }, "Cancellation-ignoring failed-start result", SignalWaitTimeout);

            TimeoutException stopException = null;
            try
            {
                await server.StopAsync();
            }
            catch (TimeoutException exception)
            {
                stopException = exception;
            }
            finally
            {
                listener.Complete();
                await TestAwaiter.WaitAsync(
                    listener.Exited,
                    "Cancellation-ignoring failed-start listener cleanup",
                    SignalWaitTimeout);
                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Completed failed-start listener cleanup",
                    SignalWaitTimeout);
            }

            Assert.That(stopException, Is.Not.Null);
        });

        private static UnityIpcServer CreateServer (
            IUnityIpcTransportListener listener,
            IDaemonLogger daemonLogger = null,
            IDaemonShutdownSignal daemonShutdownSignal = null)
        {
            return new UnityIpcServer(
                new NoOpConnectionHandler(),
                new List<IUnityIpcTransportListener>
                {
                    listener,
                },
                daemonShutdownSignal ?? new NoOpDaemonShutdownSignal(),
                daemonLogger ?? NoOpDaemonLogger.Instance,
                UnityIpcServer.DefaultListenerStopTimeout);
        }

        private static async UniTask AssertImmediateListenerTerminationCannotCommitAsync (
            bool throwAfterStartup)
        {
            var listener = new ImmediateTerminationTransportListener(throwAfterStartup);
            var server = CreateServer(listener);
            var endpoint = new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                throwAfterStartup
                    ? "ucli-generation-immediate-startup-throw"
                    : "ucli-generation-immediate-startup-return");
            IUnityIpcServerPublicationFence publicationFence = null;
            InvalidOperationException startException = null;
            try
            {
                try
                {
                    publicationFence = await TestAwaiter.WaitAsync(
                        server.StartAsync(endpoint),
                        "Immediate listener termination startup",
                        SignalWaitTimeout);
                }
                catch (InvalidOperationException exception)
                {
                    startException = exception;
                }

                Assert.That(publicationFence, Is.Null);
                Assert.That(startException, Is.Not.Null);
            }
            finally
            {
                publicationFence?.Dispose();
                await TestAwaiter.WaitAsync(
                    server.StopAsync(),
                    "Immediate listener termination cleanup",
                    SignalWaitTimeout);
            }
        }

        private static UnityIpcConnectionHandleResult CreateSuccessfulShutdownResult ()
        {
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "session-token",
                method: TextVocabulary.GetText(UnityIpcMethod.Shutdown),
                payload: IpcPayloadCodec.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: "single",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
            var response = new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: request.RequestId,
                status: IpcResponseStatus.Ok,
                payload: IpcPayloadCodec.SerializeToElement(new IpcShutdownResponse(true, "ok")),
                errors: Array.Empty<IpcError>());
            return new UnityIpcConnectionHandleResult(
                ValidatedUnityIpcRequestTestFactory.Create(request),
                response,
                isShutdownAdmissionCommitted: true);
        }

        private static async Task WaitForCancellationAsync (
            CancellationToken cancellationToken,
            TaskCompletionSource<bool> cancellationObserved)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                cancellationObserved.TrySetResult(true);
                completion.TrySetCanceled(cancellationToken);
            });
            await completion.Task;
        }

        private sealed class CancellationIgnoringTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> completion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> exited =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int runCount;

            private int releaseCallCount;

            public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

            public Task Exited => exited.Task;

            public int RunCount => Volatile.Read(ref runCount);

            public int ReleaseCallCount => Volatile.Read(ref releaseCallCount);

            public bool ThrowOnRelease { get; set; }

            public void Complete ()
            {
                completion.TrySetResult(true);
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref runCount);
                onStarted();
                try
                {
                    await completion.Task;
                }
                finally
                {
                    exited.TrySetResult(true);
                }
            }

            public void Release ()
            {
                Interlocked.Increment(ref releaseCallCount);
                if (ThrowOnRelease)
                {
                    throw new InvalidOperationException("Transport release failed.");
                }
            }
        }

        private sealed class CancellationIgnoringFailedStartTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> runEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> completion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> exited =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

            public Task RunEntered => runEntered.Task;

            public Task Exited => exited.Task;

            public void Complete ()
            {
                completion.TrySetResult(true);
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                runEntered.TrySetResult(true);
                try
                {
                    await completion.Task;
                }
                finally
                {
                    exited.TrySetResult(true);
                }
            }

            public void Release ()
            {
            }
        }

        private sealed class RecordingDisposable : IDisposable
        {
            private int disposeCallCount;

            public int DisposeCallCount => Volatile.Read(ref disposeCallCount);

            public void Dispose ()
            {
                Interlocked.Increment(ref disposeCallCount);
            }
        }

        private sealed class UnexpectedReturnTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> firstGenerationCompletion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> restartedGenerationCancellationObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int runCount;

            public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

            public int RunCount => Volatile.Read(ref runCount);

            public void CompleteFirstGeneration ()
            {
                firstGenerationCompletion.TrySetResult(true);
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                var currentRun = Interlocked.Increment(ref runCount);
                onStarted();
                if (currentRun == 1)
                {
                    await firstGenerationCompletion.Task;
                    return;
                }

                await WaitForCancellationAsync(
                    cancellationToken,
                    restartedGenerationCancellationObserved);
            }

            public void Release ()
            {
            }
        }

        private sealed class ImmediateTerminationTransportListener : IUnityIpcTransportListener
        {
            private readonly bool throwAfterStartup;

            public ImmediateTerminationTransportListener (bool throwAfterStartup)
            {
                this.throwAfterStartup = throwAfterStartup;
            }

            public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

            public Action AfterStartup { private get; set; }

            public Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                onStarted();
                AfterStartup?.Invoke();
                return throwAfterStartup
                    ? Task.FromException(new InvalidOperationException("Immediate listener failure."))
                    : Task.CompletedTask;
            }

            public void Release ()
            {
            }
        }

        private sealed class DelayedConnectionCompletionTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> firstGenerationCompletion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> firstGenerationExited =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> restartedGenerationCancellationObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private Action<UnityIpcConnectionHandleResult> firstGenerationConnectionCompleted;

            private int runCount;

            public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

            public Task FirstGenerationExited => firstGenerationExited.Task;

            public void ReportFirstGenerationCompletion (UnityIpcConnectionHandleResult result)
            {
                var completion = firstGenerationConnectionCompleted
                    ?? throw new InvalidOperationException("First listener generation completion callback is not available.");
                completion(result);
            }

            public void CompleteFirstGeneration ()
            {
                firstGenerationCompletion.TrySetResult(true);
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                var currentRun = Interlocked.Increment(ref runCount);
                onStarted();
                if (currentRun == 1)
                {
                    firstGenerationConnectionCompleted = onConnectionCompleted;
                    try
                    {
                        await firstGenerationCompletion.Task;
                    }
                    finally
                    {
                        firstGenerationExited.TrySetResult(true);
                    }

                    return;
                }

                await WaitForCancellationAsync(
                    cancellationToken,
                    restartedGenerationCancellationObserved);
            }

            public void Release ()
            {
            }
        }

        private sealed class DelayedStartupCancellationTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> firstRunEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> allowFirstGenerationCancellation =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> restartedGenerationCancellationObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int runCount;

            public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

            public Task FirstRunEntered => firstRunEntered.Task;

            public Task RestartedGenerationCancellationObserved => restartedGenerationCancellationObserved.Task;

            public void AllowFirstGenerationCancellation ()
            {
                allowFirstGenerationCancellation.TrySetResult(true);
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                var currentRun = Interlocked.Increment(ref runCount);
                if (currentRun == 1)
                {
                    firstRunEntered.TrySetResult(true);
                    await allowFirstGenerationCancellation.Task;
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Released listener generation was expected to be canceled.");
                }

                onStarted();
                await WaitForCancellationAsync(
                    cancellationToken,
                    restartedGenerationCancellationObserved);
            }

            public void Release ()
            {
            }
        }

        private sealed class DelayedReleasedGenerationStartupTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> firstRunEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> reportReleasedGenerationStartup =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> successorCancellationObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int runCount;

            public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

            public Task FirstRunEntered => firstRunEntered.Task;

            public Task SuccessorCancellationObserved => successorCancellationObserved.Task;

            public void ReportReleasedGenerationStartup ()
            {
                reportReleasedGenerationStartup.TrySetResult(true);
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                var currentRun = Interlocked.Increment(ref runCount);
                if (currentRun == 1)
                {
                    firstRunEntered.TrySetResult(true);
                    await reportReleasedGenerationStartup.Task;
                    onStarted();
                    return;
                }

                onStarted();
                await WaitForCancellationAsync(
                    cancellationToken,
                    successorCancellationObserved);
            }

            public void Release ()
            {
            }
        }

        private sealed class StartupReleaseCancellationOwnershipTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> cancellationCallbackEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> cancellationCallbackCompleted =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> listenerExited =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly ManualResetEventSlim allowCancellationCallbackCompletion = new ManualResetEventSlim();

            private int sourceDisposedDuringCancellationCallback;

            public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

            public Action ReleaseAfterStartup { private get; set; }

            public Task CancellationCallbackEntered => cancellationCallbackEntered.Task;

            public Task CancellationCallbackCompleted => cancellationCallbackCompleted.Task;

            public Task ListenerExited => listenerExited.Task;

            public bool SourceDisposedDuringCancellationCallback =>
                Volatile.Read(ref sourceDisposedDuringCancellationCallback) != 0;

            public void AllowCancellationCallbackCompletion ()
            {
                allowCancellationCallbackCompletion.Set();
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                _ = cancellationToken.Register(() =>
                {
                    cancellationCallbackEntered.TrySetResult(true);
                    allowCancellationCallbackCompletion.Wait();
                    try
                    {
                        _ = cancellationToken.WaitHandle;
                    }
                    catch (ObjectDisposedException)
                    {
                        Interlocked.Exchange(ref sourceDisposedDuringCancellationCallback, 1);
                    }
                    finally
                    {
                        cancellationCallbackCompleted.TrySetResult(true);
                    }
                });

                onStarted();
                var releaseAfterStartup = ReleaseAfterStartup
                    ?? throw new InvalidOperationException("Startup release callback is not configured.");
                releaseAfterStartup();
                await cancellationCallbackEntered.Task;
                listenerExited.TrySetResult(true);
            }

            public void Release ()
            {
            }
        }

        private sealed class DelayedFaultTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> failFirstGeneration =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> restartedGenerationCancellationObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int runCount;

            public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

            public Task RestartedGenerationCancellationObserved => restartedGenerationCancellationObserved.Task;

            public void FailFirstGeneration ()
            {
                failFirstGeneration.TrySetResult(true);
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                var currentRun = Interlocked.Increment(ref runCount);
                onStarted();
                if (currentRun == 1)
                {
                    await failFirstGeneration.Task;
                    throw new InvalidOperationException("First listener generation failed after restart.");
                }

                await WaitForCancellationAsync(
                    cancellationToken,
                    restartedGenerationCancellationObserved);
            }

            public void Release ()
            {
            }
        }

        private sealed class BlockingFailedStartCleanupTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> firstRunEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> failedStartCleanupCancellationEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> restartedGenerationReleaseObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> restartedGenerationCompletion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly ManualResetEventSlim allowFailedStartCleanupCancellation = new ManualResetEventSlim();

            private int runCount;

            private int restartedGenerationActive;

            public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

            public Task FirstRunEntered => firstRunEntered.Task;

            public Task FailedStartCleanupCancellationEntered => failedStartCleanupCancellationEntered.Task;

            public Task RestartedGenerationReleaseObserved => restartedGenerationReleaseObserved.Task;

            public void AllowFailedStartCleanupCancellation ()
            {
                allowFailedStartCleanupCancellation.Set();
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                var currentRun = Interlocked.Increment(ref runCount);
                if (currentRun == 1)
                {
                    using var cancellationRegistration = cancellationToken.Register(() =>
                    {
                        failedStartCleanupCancellationEntered.TrySetResult(true);
                        allowFailedStartCleanupCancellation.Wait();
                    });
                    firstRunEntered.TrySetResult(true);
                    await WaitForCancellationAsync(
                        cancellationToken,
                        new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
                    return;
                }

                Interlocked.Exchange(ref restartedGenerationActive, 1);
                try
                {
                    onStarted();
                    using var cancellationRegistration = cancellationToken.Register(() =>
                    {
                        restartedGenerationCompletion.TrySetCanceled(cancellationToken);
                    });
                    await restartedGenerationCompletion.Task;
                }
                finally
                {
                    Interlocked.Exchange(ref restartedGenerationActive, 0);
                }
            }

            public void Release ()
            {
                if (Volatile.Read(ref restartedGenerationActive) == 0)
                {
                    return;
                }

                restartedGenerationReleaseObserved.TrySetResult(true);
                restartedGenerationCompletion.TrySetException(
                    new ObjectDisposedException("Restarted listener transport"));
            }
        }

        private sealed class BlockingStopReleaseTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> firstRunCompletion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> restartedRunCompletion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> firstReleaseEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> restartedGenerationReleaseObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly ManualResetEventSlim allowFirstRelease = new ManualResetEventSlim();

            private int runCount;

            private int activeRun;

            private int releaseCount;

            public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

            public Task FirstReleaseEntered => firstReleaseEntered.Task;

            public Task RestartedGenerationReleaseObserved => restartedGenerationReleaseObserved.Task;

            public int ReleaseCallCount => Volatile.Read(ref releaseCount);

            public void AllowFirstRelease ()
            {
                allowFirstRelease.Set();
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                var currentRun = Interlocked.Increment(ref runCount);
                Interlocked.Exchange(ref activeRun, currentRun);
                var runCompletion = currentRun == 1
                    ? firstRunCompletion
                    : restartedRunCompletion;
                try
                {
                    onStarted();
                    using var cancellationRegistration = cancellationToken.Register(() =>
                    {
                        runCompletion.TrySetCanceled(cancellationToken);
                    });
                    await runCompletion.Task;
                }
                finally
                {
                    Interlocked.CompareExchange(ref activeRun, 0, currentRun);
                }
            }

            public void Release ()
            {
                if (Interlocked.Increment(ref releaseCount) != 1)
                {
                    return;
                }

                firstReleaseEntered.TrySetResult(true);
                allowFirstRelease.Wait();
                if (Volatile.Read(ref activeRun) != 2)
                {
                    return;
                }

                restartedGenerationReleaseObserved.TrySetResult(true);
                restartedRunCompletion.TrySetException(
                    new ObjectDisposedException("Restarted listener transport"));
            }
        }

        private sealed class LifecycleReleaseBarrierTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> stopReleaseEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> lifecycleReleaseEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly ManualResetEventSlim allowStopRelease = new ManualResetEventSlim();

            private readonly ManualResetEventSlim allowLifecycleRelease = new ManualResetEventSlim();

            private int runCount;

            private int releaseCount;

            public IpcTransportKind TransportKind => IpcTransportKind.NamedPipe;

            public Task StopReleaseEntered => stopReleaseEntered.Task;

            public Task LifecycleReleaseEntered => lifecycleReleaseEntered.Task;

            public int RunCount => Volatile.Read(ref runCount);

            public void AllowStopRelease ()
            {
                allowStopRelease.Set();
            }

            public void AllowLifecycleRelease ()
            {
                allowLifecycleRelease.Set();
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref runCount);
                onStarted();
                await WaitForCancellationAsync(
                    cancellationToken,
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            }

            public void Release ()
            {
                var currentRelease = Interlocked.Increment(ref releaseCount);
                if (currentRelease == 1)
                {
                    stopReleaseEntered.TrySetResult(true);
                    allowStopRelease.Wait();
                    return;
                }

                if (currentRelease == 2)
                {
                    lifecycleReleaseEntered.TrySetResult(true);
                    allowLifecycleRelease.Wait();
                }
            }
        }

        private sealed class ExceptionObservingDaemonLogger : IDaemonLogger
        {
            private readonly TaskCompletionSource<bool> exceptionObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task ExceptionObserved => exceptionObserved.Task;

            public void Info (
                string category,
                string message,
                string raw = null)
            {
            }

            public void Warning (
                string category,
                string message,
                string raw = null)
            {
            }

            public void Error (
                string category,
                string message,
                string raw = null)
            {
            }

            public void Exception (
                string category,
                string message,
                Exception exception)
            {
                exceptionObserved.TrySetResult(true);
            }
        }

        private sealed class BlockingExceptionDaemonLogger : IDaemonLogger
        {
            private readonly TaskCompletionSource<bool> exceptionEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly ManualResetEventSlim allowExceptionCompletion = new ManualResetEventSlim();

            public Task ExceptionEntered => exceptionEntered.Task;

            public void AllowExceptionCompletion ()
            {
                allowExceptionCompletion.Set();
            }

            public void Info (
                string category,
                string message,
                string raw = null)
            {
            }

            public void Warning (
                string category,
                string message,
                string raw = null)
            {
            }

            public void Error (
                string category,
                string message,
                string raw = null)
            {
            }

            public void Exception (
                string category,
                string message,
                Exception exception)
            {
                exceptionEntered.TrySetResult(true);
                allowExceptionCompletion.Wait();
            }
        }

        private sealed class NoOpConnectionHandler : IUnityIpcConnectionHandler
        {
            public Task<UnityIpcConnectionHandleResult> HandleAsync (
                Stream stream,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class NoOpDaemonShutdownSignal : IDaemonShutdownSignal
        {
            public bool IsSignaled => false;

            public void Signal ()
            {
            }

            public Task WaitAsync (CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingDaemonShutdownSignal : IDaemonShutdownSignal
        {
            private int signalCount;

            public bool IsSignaled => SignalCount > 0;

            public int SignalCount => Volatile.Read(ref signalCount);

            public void Signal ()
            {
                Interlocked.Increment(ref signalCount);
            }

            public Task WaitAsync (CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }
    }
}
