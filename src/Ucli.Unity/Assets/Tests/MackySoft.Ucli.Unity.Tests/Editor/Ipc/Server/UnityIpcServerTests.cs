using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Unity.Execution.Dispatch;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcServerTests
    {
        private const int MaximumActiveConnections = 32;

        private const string CanonicalSessionToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        private static readonly TimeSpan ConnectionDrainTimeout = TimeSpan.FromSeconds(1);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartupCoordinator_Wait_WhenStartupCompletesBeforeCancellation_DoesNotThrow () => UniTask.ToCoroutine(async () =>
        {
            var startupCoordinator = new UnityIpcServerStartupCoordinator();
            using var cancellationTokenSource = new CancellationTokenSource();
            var waitTask = startupCoordinator.WaitAsync(cancellationTokenSource.Token);

            startupCoordinator.MarkListenerLifetimeTracked();
            startupCoordinator.SignalListenerStarted();
            cancellationTokenSource.Cancel();

            await TestAwaiter.WaitAsync(waitTask, "Startup completion before cancellation", SignalWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartupCoordinator_Wait_WhenStartupCompletesShortlyAfterCancellation_DoesNotThrow () => UniTask.ToCoroutine(async () =>
        {
            var startupCoordinator = new UnityIpcServerStartupCoordinator();
            using var cancellationTokenSource = new CancellationTokenSource();
            startupCoordinator.MarkListenerLifetimeTracked();
            using var completionRegistration = cancellationTokenSource.Token.Register(startupCoordinator.SignalListenerStarted);
            var waitTask = startupCoordinator.WaitAsync(cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            await TestAwaiter.WaitAsync(waitTask, "Startup completion after cancellation", SignalWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartupCoordinator_Wait_WhenListenerStartsBeforeLifetimeTracking_WaitsForTracking () => UniTask.ToCoroutine(async () =>
        {
            var startupCoordinator = new UnityIpcServerStartupCoordinator();
            var waitTask = startupCoordinator.WaitAsync(CancellationToken.None);

            startupCoordinator.SignalListenerStarted();

            Assert.That(waitTask.IsCompleted, Is.False);

            startupCoordinator.MarkListenerLifetimeTracked();

            await TestAwaiter.WaitAsync(waitTask, "Startup listener lifetime tracking", SignalWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartupCoordinator_Wait_WhenCanceledWithoutStartup_ThrowsOperationCanceledException () => UniTask.ToCoroutine(async () =>
        {
            var startupCoordinator = new UnityIpcServerStartupCoordinator();
            using var cancellationTokenSource = new CancellationTokenSource();
            var waitTask = startupCoordinator.WaitAsync(cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await waitTask.AsUniTask();
            }, "Startup cancellation result", SignalWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenEndpointIsNull_ThrowsArgumentNullException () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForLifecycle();
            var exception = await AsyncExceptionCapture.CaptureAsync<ArgumentNullException>(async () =>
            {
                await server.StartAsync(null).AsUniTask();
            }, "Null endpoint start", SignalWaitTimeout);

            Assert.That(exception.ParamName, Is.EqualTo("endpoint"));
        });

        [Test]
        [Category("Size.Small")]
        public void IpcEndpointConstructor_WhenAddressIsWhitespace_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() => new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                " "));

            Assert.That(exception.ParamName, Is.EqualTo("address"));
        }

        [Test]
        [Category("Size.Small")]
        public void IpcEndpointConstructor_WhenTransportIsUnsupported_ThrowsArgumentOutOfRangeException ()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new IpcEndpoint(
                (IpcTransportKind)int.MaxValue,
                "ucli-unsupported-transport"));

            Assert.That(exception.ParamName, Is.EqualTo("transportKind"));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_ThenStop_TransitionsRunningState () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForLifecycle();
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test");
            await TestAwaiter.WaitAsync(
                server.StartAsync(endpoint).AsUniTask(),
                "Server lifecycle start",
                SignalWaitTimeout);
            Assert.That(server.IsRunning, Is.True);

            await TestAwaiter.WaitAsync(
                server.StopAsync().AsUniTask(),
                "Server lifecycle stop",
                SignalWaitTimeout);
            Assert.That(server.IsRunning, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnixDomainSocketListener_Run_WhenUsingFallbackEndpoint_AppliesOwnerOnlyBoundaryAndCleansUp () => UniTask.ToCoroutine(async () =>
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return;
            }

            var socketDirectoryPath = Path.Combine(Path.GetTempPath(), UcliIpcEndpointNames.DaemonAddressPrefix + Guid.NewGuid().ToString("N"));
            var address = Path.Combine(socketDirectoryPath, UcliIpcEndpointNames.UnixSocketFileName);
            var listener = new UnixDomainSocketUnityIpcTransportListener(
                NoOpDaemonLogger.Instance,
                MaximumActiveConnections,
                ConnectionDrainTimeout);
            var startedTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationTokenSource = new CancellationTokenSource();

            var runTask = listener.RunAsync(
                address,
                new StubConnectionHandler(),
                () => startedTaskSource.TrySetResult(true),
                _ => { },
                cancellationTokenSource.Token);

            try
            {
                await TestAwaiter.WaitAsync(startedTaskSource.Task, "Unix domain socket listener start", SignalWaitTimeout);

                Assert.That(Directory.Exists(socketDirectoryPath), Is.True);
                Assert.That(File.Exists(address), Is.True);
                Assert.That(await ReadUnixFileModeAsync(socketDirectoryPath), Is.EqualTo("0700"));
                Assert.That(await ReadUnixFileModeAsync(address), Is.EqualTo("0600"));
            }
            finally
            {
                cancellationTokenSource.Cancel();
                listener.Release();

                try
                {
                    await TestAwaiter.WaitAsync(runTask, "Unix domain socket listener shutdown", SignalWaitTimeout);
                }
                catch (OperationCanceledException)
                {
                }
            }

            Assert.That(File.Exists(address), Is.False);
            Assert.That(Directory.Exists(socketDirectoryPath), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator NamedPipeListener_Run_WhenConnectionHandled_ReportsCompletionAfterConnectionClosed () => UniTask.ToCoroutine(async () =>
        {
            var address = "ucli-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var listener = new NamedPipeUnityIpcTransportListener(
                NoOpDaemonLogger.Instance,
                MaximumActiveConnections,
                ConnectionDrainTimeout);
            var connectionHandler = new ShutdownResultConnectionHandler();
            var startedTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connectionCompletedTaskSource = new TaskCompletionSource<UnityIpcConnectionHandleResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationTokenSource = new CancellationTokenSource();

            var runTask = listener.RunAsync(
                address,
                connectionHandler,
                () => startedTaskSource.TrySetResult(true),
                result => connectionCompletedTaskSource.TrySetResult(result),
                cancellationTokenSource.Token);

            try
            {
                await TestAwaiter.WaitAsync(startedTaskSource.Task, "Named pipe listener start", SignalWaitTimeout);

                using var clientStream = new NamedPipeClientStream(".", address, PipeDirection.InOut, PipeOptions.Asynchronous);
                clientStream.Connect((int)SignalWaitTimeout.TotalMilliseconds);

                var result = await TestAwaiter.WaitAsync(connectionCompletedTaskSource.Task, "Named pipe connection completion", SignalWaitTimeout);
                var readBuffer = new byte[1];
                var bytesRead = await TestAwaiter.WaitAsync(
                    clientStream.ReadAsync(readBuffer, 0, readBuffer.Length),
                    "Named pipe client EOF",
                    SignalWaitTimeout);

                Assert.That(result.Request, Is.Not.Null);
                Assert.That(result.Request.Method, Is.EqualTo(ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown)));
                Assert.That(result.Response, Is.Not.Null);
                Assert.That(result.Response.Status, Is.EqualTo(IpcProtocol.StatusOk));
                Assert.That(result.Response.Errors, Is.Empty);
                Assert.That(bytesRead, Is.EqualTo(0));
            }
            finally
            {
                cancellationTokenSource.Cancel();
                listener.Release();

                try
                {
                    await TestAwaiter.WaitAsync(runTask, "Named pipe listener shutdown", SignalWaitTimeout);
                }
                catch (OperationCanceledException)
                {
                }
            }

            Assert.That(connectionHandler.CallCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator NamedPipeListener_Run_WhenFirstConnectionIsStillHandling_AcceptsSecondConnection () => UniTask.ToCoroutine(async () =>
        {
            var address = "ucli-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var listener = new NamedPipeUnityIpcTransportListener(
                NoOpDaemonLogger.Instance,
                MaximumActiveConnections,
                ConnectionDrainTimeout);
            var connectionHandler = new BlockingConnectionHandler();
            var startedTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationTokenSource = new CancellationTokenSource();

            var runTask = listener.RunAsync(
                address,
                connectionHandler,
                () => startedTaskSource.TrySetResult(true),
                _ => { },
                cancellationTokenSource.Token);

            try
            {
                await TestAwaiter.WaitAsync(startedTaskSource.Task, "Named pipe listener start", SignalWaitTimeout);

                using var firstClientStream = new NamedPipeClientStream(".", address, PipeDirection.InOut, PipeOptions.Asynchronous);
                firstClientStream.Connect((int)SignalWaitTimeout.TotalMilliseconds);
                await TestAwaiter.WaitAsync(connectionHandler.FirstConnectionObserved, "First named pipe connection handling", SignalWaitTimeout);

                using var secondClientStream = new NamedPipeClientStream(".", address, PipeDirection.InOut, PipeOptions.Asynchronous);
                secondClientStream.Connect((int)SignalWaitTimeout.TotalMilliseconds);
                await TestAwaiter.WaitAsync(connectionHandler.SecondConnectionObserved, "Second named pipe connection handling", SignalWaitTimeout);
            }
            finally
            {
                connectionHandler.Release();
                cancellationTokenSource.Cancel();
                listener.Release();

                try
                {
                    await TestAwaiter.WaitAsync(runTask, "Named pipe listener shutdown", SignalWaitTimeout);
                }
                catch (OperationCanceledException)
                {
                }
            }

            Assert.That(connectionHandler.CallCount, Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ConnectionGroup_TryStart_WhenActiveConnectionLimitIsReached_RejectsAndClosesOverflowHandle () => UniTask.ToCoroutine(async () =>
        {
            const int connectionLimit = 2;
            var connectionGroup = new UnityIpcTransportConnectionGroup(
                NoOpDaemonLogger.Instance,
                connectionLimit);
            var handlerCompletion = new TaskCompletionSource<UnityIpcConnectionHandleResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var admittedHandlersEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var admittedHandlerCallCount = 0;
            var overflowHandlerCallCount = 0;
            var admittedHandles = new[]
            {
                new RecordingTransportHandle(),
                new RecordingTransportHandle(),
            };
            var overflowHandle = new RecordingTransportHandle();

            try
            {
                foreach (var admittedHandle in admittedHandles)
                {
                    var admitted = connectionGroup.TryStart(
                        admittedHandle,
                        async () =>
                        {
                            if (Interlocked.Increment(ref admittedHandlerCallCount) == connectionLimit)
                            {
                                admittedHandlersEntered.TrySetResult(true);
                            }

                            return await handlerCompletion.Task;
                        },
                        _ => { },
                        CancellationToken.None);
                    Assert.That(admitted, Is.True);
                }

                await TestAwaiter.WaitAsync(
                    admittedHandlersEntered.Task,
                    "Admitted connection handlers",
                    SignalWaitTimeout);

                var overflowAdmitted = connectionGroup.TryStart(
                    overflowHandle,
                    () =>
                    {
                        Interlocked.Increment(ref overflowHandlerCallCount);
                        return Task.FromResult(default(UnityIpcConnectionHandleResult));
                    },
                    _ => { },
                    CancellationToken.None);

                Assert.That(overflowAdmitted, Is.False);
                Assert.That(overflowHandle.DisposeCallCount, Is.EqualTo(1));
                Assert.That(Volatile.Read(ref overflowHandlerCallCount), Is.EqualTo(0));
                Assert.That(Volatile.Read(ref admittedHandlerCallCount), Is.EqualTo(connectionLimit));
            }
            finally
            {
                connectionGroup.Release();
                handlerCompletion.TrySetResult(default);
                await TestAwaiter.WaitAsync(
                    connectionGroup.WaitForCompletionAsync(SignalWaitTimeout),
                    "Connection-limit test cleanup",
                    SignalWaitTimeout);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ConnectionGroup_Release_WhenTransportHandleDisposeBlocks_ReturnsBeforeCleanupCompletes () => UniTask.ToCoroutine(async () =>
        {
            var connectionGroup = new UnityIpcTransportConnectionGroup(
                NoOpDaemonLogger.Instance,
                maximumActiveConnections: 1);
            var transportHandle = new BlockingDisposeTransportHandle();
            var handlerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handlerCompletion = new TaskCompletionSource<UnityIpcConnectionHandleResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task releaseTask = null;
            var releaseCompletedBeforeDispose = false;

            try
            {
                Assert.That(connectionGroup.TryStart(
                    transportHandle,
                    async () =>
                    {
                        handlerEntered.TrySetResult(true);
                        return await handlerCompletion.Task;
                    },
                    _ => { },
                    CancellationToken.None), Is.True);
                await TestAwaiter.WaitAsync(
                    handlerEntered.Task,
                    "Blocking-dispose connection handler",
                    SignalWaitTimeout);

                releaseTask = Task.Run(connectionGroup.Release);
                await TestAwaiter.WaitAsync(
                    transportHandle.DisposeStarted,
                    "Asynchronous transport cleanup start",
                    SignalWaitTimeout);
                await TestAwaiter.WaitAsync(
                    releaseTask,
                    "Non-blocking connection group release",
                    SignalWaitTimeout);
                releaseCompletedBeforeDispose = !transportHandle.DisposeCompleted;
            }
            finally
            {
                transportHandle.AllowDispose();
                handlerCompletion.TrySetResult(default);
                if (releaseTask != null)
                {
                    await TestAwaiter.WaitAsync(
                        releaseTask,
                        "Connection group release cleanup",
                        SignalWaitTimeout);
                }

                await TestAwaiter.WaitAsync(
                    connectionGroup.WaitForCompletionAsync(SignalWaitTimeout),
                    "Blocking-dispose connection cleanup",
                    SignalWaitTimeout);
            }

            Assert.That(releaseCompletedBeforeDispose, Is.True);
            Assert.That(transportHandle.DisposeCallCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ConnectionGroup_WaitForCompletion_WhenTransportCleanupDoesNotComplete_StopsAtDrainDeadline () => UniTask.ToCoroutine(async () =>
        {
            var connectionGroup = new UnityIpcTransportConnectionGroup(
                NoOpDaemonLogger.Instance,
                maximumActiveConnections: 1);
            var transportHandle = new BlockingDisposeTransportHandle();
            TimeoutException timeoutException = null;

            try
            {
                Assert.That(connectionGroup.TryStart(
                    transportHandle,
                    () => Task.FromResult(default(UnityIpcConnectionHandleResult)),
                    _ => { },
                    CancellationToken.None), Is.True);
                await TestAwaiter.WaitAsync(
                    transportHandle.DisposeStarted,
                    "Blocking transport cleanup start",
                    SignalWaitTimeout);

                try
                {
                    await connectionGroup.WaitForCompletionAsync(TimeSpan.FromMilliseconds(25));
                }
                catch (TimeoutException exception)
                {
                    timeoutException = exception;
                }
            }
            finally
            {
                connectionGroup.Release();
                transportHandle.AllowDispose();
                await TestAwaiter.WaitAsync(
                    connectionGroup.WaitForCompletionAsync(SignalWaitTimeout),
                    "Blocking transport cleanup release",
                    SignalWaitTimeout);
            }

            Assert.That(timeoutException, Is.Not.Null);
            Assert.That(transportHandle.DisposeCallCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ConnectionGroup_WaitForCompletion_WhenConnectionCompletesAfterDeadlineWinner_DoesNotReverseTimeoutToSuccess () => UniTask.ToCoroutine(async () =>
        {
            var connectionGroup = new UnityIpcTransportConnectionGroup(
                NoOpDaemonLogger.Instance,
                maximumActiveConnections: 1);
            var transportHandle = new RecordingTransportHandle();
            var handlerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handlerCompletion = new TaskCompletionSource<UnityIpcConnectionHandleResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.That(connectionGroup.TryStart(
                transportHandle,
                async () =>
                {
                    handlerEntered.TrySetResult(true);
                    return await handlerCompletion.Task;
                },
                _ => { },
                CancellationToken.None), Is.True);
            await TestAwaiter.WaitAsync(
                handlerEntered.Task,
                "Connection handler entry",
                SignalWaitTimeout);

            var synchronizationContext = new ManuallyPumpedSynchronizationContext();
            var originalSynchronizationContext = SynchronizationContext.Current;
            Task drainTask;
            try
            {
                SynchronizationContext.SetSynchronizationContext(synchronizationContext);
                drainTask = connectionGroup.WaitForCompletionAsync(TimeSpan.FromMilliseconds(25));
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(originalSynchronizationContext);
            }

            try
            {
                Assert.That(
                    synchronizationContext.WaitForCallback(SignalWaitTimeout),
                    Is.True,
                    "Drain deadline continuation was not queued.");
                handlerCompletion.TrySetResult(default);
                await TestAwaiter.WaitAsync(
                    connectionGroup.WaitForCompletionAsync(SignalWaitTimeout),
                    "Connection completion after the drain deadline won",
                    SignalWaitTimeout);
                Assert.That(transportHandle.DisposeCallCount, Is.EqualTo(1));

                synchronizationContext.ExecuteOldestCallback();

                Assert.That(drainTask.IsFaulted, Is.True);
                Assert.That(drainTask.Exception?.GetBaseException(), Is.TypeOf<TimeoutException>());
            }
            finally
            {
                connectionGroup.Release();
                handlerCompletion.TrySetResult(default);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenTransportReportsShutdownCompletion_SignalsShutdown () => UniTask.ToCoroutine(async () =>
        {
            var request = CreateShutdownRequest(CanonicalSessionToken, Guid.NewGuid());
            var response = new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: request.RequestId,
                status: IpcProtocol.StatusOk,
                payload: JsonSerializer.SerializeToElement(new IpcShutdownResponse(true, "ok"), SerializerOptions),
                errors: Array.Empty<IpcError>());
            var listener = new CompletionReportingTransportListener(
                IpcTransportKind.NamedPipe,
                new UnityIpcConnectionHandleResult(
                    request,
                    response,
                    isShutdownAdmissionCommitted: true));
            var shutdownSignal = new StubDaemonShutdownSignal();
            var server = CreateServer(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                shutdownSignal,
                new IUnityIpcTransportListener[]
                {
                    listener,
                });
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test-shutdown-complete");

            try
            {
                await TestAwaiter.WaitAsync(
                    server.StartAsync(endpoint).AsUniTask(),
                    "Server start before shutdown completion",
                    SignalWaitTimeout);
                await TestAwaiter.WaitAsync(shutdownSignal.SignalObserved, "Server shutdown signal", SignalWaitTimeout);
            }
            finally
            {
                await TestAwaiter.WaitAsync(
                    server.StopAsync().AsUniTask(),
                    "Server stop after shutdown completion",
                    SignalWaitTimeout);
            }

            Assert.That(listener.ConnectionCompletedCallCount, Is.EqualTo(1));
            Assert.That(shutdownSignal.SignalCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnixDomainSocketListener_Run_WhenAddressExceedsSupportedByteLength_ThrowsArgumentException () => UniTask.ToCoroutine(async () =>
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return;
            }

            var address = CreateSocketPathWithByteLength(IpcTransportConstraints.UnixDomainSocketPathMaxBytes + 1);
            var listener = new UnixDomainSocketUnityIpcTransportListener(
                NoOpDaemonLogger.Instance,
                MaximumActiveConnections,
                ConnectionDrainTimeout);
            var exception = await AsyncExceptionCapture.CaptureAsync<ArgumentException>(async () =>
            {
                await listener.RunAsync(
                        address,
                        new StubConnectionHandler(),
                        () => { },
                        _ => { },
                        CancellationToken.None)
                    .AsUniTask();
            }, "Overlong unix socket address", SignalWaitTimeout);

            Assert.That(exception.ParamName, Is.EqualTo("address"));
            Assert.That(exception.Message, Does.Contain("Unix domain socket path exceeds"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Stop_WhenCanceled_ThrowsOperationCanceledException () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForLifecycle();
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await server.StopAsync(cancellationTokenSource.Token).AsUniTask();
            }, "Canceled server stop", SignalWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Stop_WhenCalledUnderSynchronizationContext_DoesNotPostContinuation () => UniTask.ToCoroutine(async () =>
        {
            var listener = new BlockingTransportListener(IpcTransportKind.NamedPipe, signalStarted: true);
            var server = CreateServer(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal(),
                new IUnityIpcTransportListener[]
                {
                    listener,
                });
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test-stop-context");

            await TestAwaiter.WaitAsync(
                server.StartAsync(endpoint).AsUniTask(),
                "Server start before stop",
                SignalWaitTimeout);
            await TestAwaiter.WaitAsync(listener.RunEntered, "Blocking transport listener entry", SignalWaitTimeout);

            var synchronizationContext = new RecordingSynchronizationContext();
            var stopTask = Task.Run(async () =>
            {
                var previousContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(synchronizationContext);
                try
                {
                    await server.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            });

            await TestAwaiter.WaitAsync(
                stopTask.AsUniTask(),
                "Server stop under synchronization context",
                SignalWaitTimeout);

            Assert.That(synchronizationContext.PostCallCount, Is.EqualTo(0));
            Assert.That(server.IsRunning, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ReleaseForEditorLifecycleEvent_WhenListenerDoesNotComplete_ReleasesTransportWithoutWaiting () => UniTask.ToCoroutine(async () =>
        {
            var listener = new NonCompletingTransportListener(IpcTransportKind.NamedPipe);
            var server = CreateServer(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal(),
                new IUnityIpcTransportListener[]
                {
                    listener,
                });
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test-lifecycle-release");

            await TestAwaiter.WaitAsync(
                server.StartAsync(endpoint).AsUniTask(),
                "Server start before lifecycle release",
                SignalWaitTimeout);
            await TestAwaiter.WaitAsync(listener.RunEntered, "Non-completing transport listener entry", SignalWaitTimeout);

            server.ReleaseForEditorLifecycleEvent();

            Assert.That(listener.ReleaseCallCount, Is.EqualTo(1));
            Assert.That(server.IsRunning, Is.False);

            listener.Complete();
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenListenerThrows_ThrowsAndResetsRunningState () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServer(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal(),
                new IUnityIpcTransportListener[]
                {
                    new ThrowingTransportListener(IpcTransportKind.NamedPipe, "listener failed"),
                });
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test-failure");

            await AsyncExceptionCapture.CaptureAsync<InvalidOperationException>(async () =>
            {
                await server.StartAsync(endpoint).AsUniTask();
            }, "Immediate listener failure on start", SignalWaitTimeout);

            Assert.That(server.IsRunning, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenListenerThrowsAfterDelayBeforeStartupSignal_ThrowsAndResetsRunningState () => UniTask.ToCoroutine(async () =>
        {
            var listener = new DelayedThrowingTransportListener(
                IpcTransportKind.NamedPipe,
                "listener failed after delay");
            var server = CreateServer(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal(),
                new IUnityIpcTransportListener[]
                {
                    listener,
                });
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test-delayed-failure");
            var startTask = server.StartAsync(endpoint).AsUniTask();

            await TestAwaiter.WaitAsync(listener.RunEntered, "Delayed fault listener entry", SignalWaitTimeout);
            listener.ReleaseFault();

            await AsyncExceptionCapture.CaptureAsync<InvalidOperationException>(async () =>
            {
                await TestAwaiter.WaitAsync(startTask, "Delayed listener failure result", SignalWaitTimeout);
            }, "Delayed listener failure result", SignalWaitTimeout);

            Assert.That(server.IsRunning, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenCanceledBeforeStartupSignal_CancelsListenerLoop () => UniTask.ToCoroutine(async () =>
        {
            var blockingListener = new BlockingTransportListener(IpcTransportKind.NamedPipe);
            var server = CreateServer(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal(),
                new IUnityIpcTransportListener[]
                {
                    blockingListener,
                });
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test-start-cancel");
            using var cancellationTokenSource = new CancellationTokenSource();

            var startTask = server.StartAsync(endpoint, cancellationTokenSource.Token);
            await TestAwaiter.WaitAsync(blockingListener.RunEntered, "Blocking transport listener entry", SignalWaitTimeout);
            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await TestAwaiter.WaitAsync(startTask.AsUniTask(), "Canceled listener startup result", SignalWaitTimeout);
            }, "Canceled listener startup result", SignalWaitTimeout);

            await TestAwaiter.WaitAsync(blockingListener.CancellationObserved, "Blocking transport listener cancellation", SignalWaitTimeout);
            Assert.That(server.IsRunning, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator WaitForTermination_WhenListenerFaultsAfterStartupSignal_Throws () => UniTask.ToCoroutine(async () =>
        {
            var listener = new StartedThenThrowingTransportListener(
                IpcTransportKind.NamedPipe,
                "listener failed after startup");
            var server = CreateServer(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal(),
                new IUnityIpcTransportListener[]
                {
                    listener,
                });
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test-fault-after-startup");

            await TestAwaiter.WaitAsync(
                server.StartAsync(endpoint).AsUniTask(),
                "Server start before termination fault test",
                SignalWaitTimeout);
            listener.ReleaseFault();
            await AsyncExceptionCapture.CaptureAsync<InvalidOperationException>(async () =>
            {
                await TestAwaiter.WaitAsync(
                    server.WaitForTerminationAsync(CancellationToken.None).AsUniTask(),
                    "Listener fault termination result",
                    SignalWaitTimeout);
            }, "Listener fault termination result", SignalWaitTimeout);

            Assert.That(server.IsRunning, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenSessionTokenIsMissing_ReturnsSessionTokenRequiredError () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = CreateRequestHandler(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService());
            var request = CreatePingRequest(sessionToken: string.Empty);

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcSessionErrorCodes.SessionTokenRequired));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenSessionTokenIsInvalid_ReturnsSessionTokenInvalidError () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = CreateRequestHandler(
                new StubSessionTokenValidator(accepted: false),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService());
            var request = CreatePingRequest(sessionToken: CanonicalSessionToken);

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcSessionErrorCodes.SessionTokenInvalid));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenPermitAllValidatorReceivesNonCanonicalToken_ReturnsSessionTokenInvalidError () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = CreateRequestHandler(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService());
            var request = CreatePingRequest(sessionToken: "not-canonical");

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcSessionErrorCodes.SessionTokenInvalid));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenSessionTokenValidationThrows_ReturnsInternalError () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = CreateRequestHandler(
                new ThrowingSessionTokenValidator(new IOException("session file read failed")),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService());
            var request = CreatePingRequest(sessionToken: CanonicalSessionToken);

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenValidTokenAndPing_ReturnsPingResponse () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = CreateRequestHandler(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService());
            var request = CreatePingRequest(sessionToken: CanonicalSessionToken);

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            var payload = response.Payload.Deserialize<IpcPingResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.EditorMode, Is.EqualTo("batchmode"));
            Assert.That(string.IsNullOrWhiteSpace(payload.UnityVersion), Is.False);
            Assert.That(string.IsNullOrWhiteSpace(payload.ServerVersion), Is.False);
            var expectedServerVersion = new AssemblyServerVersionProvider().GetVersion();
            Assert.That(payload.ServerVersion, Is.EqualTo(expectedServerVersion));
            Assert.That(Regex.IsMatch(payload.ServerVersion, "^[0-9]+\\.[0-9]+\\.[0-9]+(\\.[0-9]+)?$"), Is.True);
            Assert.That(
                payload.CompileState == IpcCompileStateCodec.Ready
                || payload.CompileState == IpcCompileStateCodec.Compiling,
                Is.True);
            Assert.That(string.IsNullOrWhiteSpace(payload.LifecycleState), Is.False);
            Assert.That(IpcEditorLifecycleStateCodec.TryParse(payload.LifecycleState, out _), Is.True);
            if (!string.IsNullOrWhiteSpace(payload.BlockingReason))
            {
                Assert.That(IpcEditorBlockingReasonCodec.TryParse(payload.BlockingReason, out _), Is.True);
            }

            Assert.That(string.IsNullOrWhiteSpace(payload.CompileGeneration), Is.False);
            Assert.That(string.IsNullOrWhiteSpace(payload.DomainReloadGeneration), Is.False);
            Assert.That(
                payload.CanAcceptExecutionRequests,
                Is.EqualTo(string.Equals(payload.LifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal)));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenValidTokenAndExecute_CallsDispatcher () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = new StubExecuteRequestDispatcher();
            var requestHandler = CreateRequestHandler(
                new StubSessionTokenValidator(accepted: true),
                dispatcher,
                new StubUnityTestRunService());
            var requestId = Guid.NewGuid();
            var request = CreateExecuteRequest(sessionToken: CanonicalSessionToken, requestId);

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(dispatcher.CallCount, Is.EqualTo(1));
            Assert.That(dispatcher.LastContext, Is.Not.Null);
            Assert.That(dispatcher.LastContext.RequestId, Is.EqualTo(requestId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenShutdownAccepted_ReturnsAcceptedResponse () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = CreateRequestHandler(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService());
            var request = CreateShutdownRequest(sessionToken: CanonicalSessionToken, Guid.NewGuid());

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            var payload = response.Payload.Deserialize<IpcShutdownResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Accepted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenValidTokenAndTestRun_CallsTestRunService () => UniTask.ToCoroutine(async () =>
        {
            var testRunService = new StubUnityTestRunService(new IpcTestRunResponse(2));
            var requestHandler = CreateRequestHandler(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                testRunService);
            var request = CreateTestRunRequest(sessionToken: CanonicalSessionToken, Guid.NewGuid(), failFast: true);

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(testRunService.CallCount, Is.EqualTo(1));
            Assert.That(testRunService.LastRequest, Is.Not.Null);
            Assert.That(testRunService.LastRequest.TestPlatform, Is.EqualTo("editmode"));
            Assert.That(testRunService.LastRequest.FailFast, Is.True);
            var payload = response.Payload.Deserialize<IpcTestRunResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.ExitCode, Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenTestRunServiceReturnsLifecycleFailure_PreservesErrorCode () => UniTask.ToCoroutine(async () =>
        {
            var testRunService = new StubUnityTestRunService(UnityTestRunServiceResult.Failure(
                new IpcError(EditorLifecycleErrorCodes.EditorBusy, "Unity editor is busy with internal work.", null)));
            var requestHandler = CreateRequestHandler(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                testRunService);
            var request = CreateTestRunRequest(sessionToken: CanonicalSessionToken, Guid.NewGuid());

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
            Assert.That(response.Errors[0].Message, Is.EqualTo("Unity editor is busy with internal work."));
            Assert.That(testRunService.CallCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenTestRunPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = CreateRequestHandler(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService());
            var invalidPayload = JsonSerializer.SerializeToElement(123, SerializerOptions);
            var request = new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: CanonicalSessionToken,
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.TestRun),
                payload: invalidPayload,
                responseMode: "single");

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenValidTokenAndDaemonLogsRead_ReturnsDaemonLogEvents () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = CreateRequestHandler(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService());
            var request = CreateDaemonLogsReadRequest(sessionToken: CanonicalSessionToken, Guid.NewGuid());

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            var payload = response.Payload.Deserialize<IpcDaemonLogsReadResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Events.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(payload.NextCursor, Is.Not.Empty);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenValidTokenAndUnityLogsRead_ReturnsUnityLogEvents () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = CreateRequestHandler(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService());
            var request = CreateUnityLogsReadRequest(sessionToken: CanonicalSessionToken, Guid.NewGuid());

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            var payload = response.Payload.Deserialize<IpcUnityLogsReadResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Events.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(payload.Events[0].Source, Is.EqualTo("runtime"));
            Assert.That(payload.NextCursor, Is.Not.Empty);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ProcessRequest_WhenValidTokenAndUnityConsoleClear_ReturnsSuccessResponse () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = CreateRequestHandler(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService());
            var request = CreateUnityConsoleClearRequest(sessionToken: CanonicalSessionToken, Guid.NewGuid());

            var response = await requestHandler.HandleAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            var payload = response.Payload.Deserialize<IpcUnityConsoleClearResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
        });

        private static IpcRequest CreatePingRequest (string sessionToken)
        {
            return new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: sessionToken,
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Ping),
                payload: JsonSerializer.SerializeToElement(new IpcPingRequest("tests"), SerializerOptions),
                responseMode: "single");
        }

        private static IpcRequest CreateExecuteRequest (
            string sessionToken,
            Guid requestId)
        {
            var arguments = JsonSerializer.SerializeToElement(
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    ops = Array.Empty<object>(),
                },
                SerializerOptions);
            var payload = JsonSerializer.SerializeToElement(
                new IpcExecuteRequest(UcliCommandIds.Validate, arguments),
                SerializerOptions);
            return new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: sessionToken,
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Execute),
                payload: payload,
                responseMode: "single");
        }

        private static IpcRequest CreateShutdownRequest (
            string sessionToken,
            Guid requestId)
        {
            var payload = JsonSerializer.SerializeToElement(
                new IpcShutdownRequest("tests"),
                SerializerOptions);
            return new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: sessionToken,
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown),
                payload: payload,
                responseMode: "single");
        }

        private static IpcRequest CreateTestRunRequest (
            string sessionToken,
            Guid requestId,
            bool failFast = false)
        {
            var payload = JsonSerializer.SerializeToElement(
                new IpcTestRunRequest(
                    TestPlatform: TestRunPlatformCodec.EditMode,
                    TestFilter: null,
                    TestCategories: Array.Empty<string>(),
                    AssemblyNames: Array.Empty<string>(),
                    TestSettingsPath: null,
                    ResultsXmlPath: "/tmp/results.xml",
                    EditorLogPath: "/tmp/editor.log",
                    FailFast: failFast,
                    RunId: "run-id"),
                SerializerOptions);
            return new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: sessionToken,
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.TestRun),
                payload: payload,
                responseMode: "single");
        }

        private static IpcRequest CreateDaemonLogsReadRequest (
            string sessionToken,
            Guid requestId)
        {
            var payload = JsonSerializer.SerializeToElement(
                new IpcDaemonLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Category: null),
                SerializerOptions);
            return new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: sessionToken,
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.DaemonLogsRead),
                payload: payload,
                responseMode: "single");
        }

        private static IpcRequest CreateUnityLogsReadRequest (
            string sessionToken,
            Guid requestId)
        {
            var payload = JsonSerializer.SerializeToElement(
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Source: null,
                    StackTrace: "all",
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null),
                SerializerOptions);
            return new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: sessionToken,
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.UnityLogsRead),
                payload: payload,
                responseMode: "single");
        }

        private static IpcRequest CreateUnityConsoleClearRequest (
            string sessionToken,
            Guid requestId)
        {
            var payload = JsonSerializer.SerializeToElement(
                new IpcUnityConsoleClearRequest("tests"),
                SerializerOptions);
            return new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: sessionToken,
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.UnityConsoleClear),
                payload: payload,
                responseMode: "single");
        }

        private static UnityIpcServer CreateServerForLifecycle ()
        {
            var shutdownSignal = new StubDaemonShutdownSignal();
            return CreateServer(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                shutdownSignal,
                new IUnityIpcTransportListener[]
                {
                    new NamedPipeUnityIpcTransportListener(
                        NoOpDaemonLogger.Instance,
                        MaximumActiveConnections,
                        ConnectionDrainTimeout),
                    new UnixDomainSocketUnityIpcTransportListener(
                        NoOpDaemonLogger.Instance,
                        MaximumActiveConnections,
                        ConnectionDrainTimeout),
                });
        }

        private static UnityIpcRequestHandler CreateRequestHandler (
            ISessionTokenValidator sessionTokenValidator,
            IExecuteRequestDispatcher executeRequestDispatcher,
            IUnityTestRunService testRunService)
        {
            return CreateRequestHandler(
                sessionTokenValidator,
                executeRequestDispatcher,
                testRunService,
                new TestShutdownAdmissionCoordinator());
        }

        private static UnityIpcServer CreateServer (
            ISessionTokenValidator sessionTokenValidator,
            IExecuteRequestDispatcher executeRequestDispatcher,
            IUnityTestRunService testRunService,
            IDaemonShutdownSignal shutdownSignal,
            IReadOnlyList<IUnityIpcTransportListener> transportListeners)
        {
            var shutdownAdmissionCoordinator = new TestShutdownAdmissionCoordinator();
            var requestHandler = CreateRequestHandler(
                sessionTokenValidator,
                executeRequestDispatcher,
                testRunService,
                shutdownAdmissionCoordinator);
            var connectionHandler = new UnityIpcConnectionHandler(
                requestHandler,
                shutdownAdmissionCoordinator,
                UnityIpcConnectionHandler.DefaultInitialFrameReadTimeout,
                UnityIpcConnectionHandler.DefaultResponseFrameWriteTimeout);
            return new UnityIpcServer(
                connectionHandler,
                transportListeners,
                shutdownSignal,
                NoOpDaemonLogger.Instance,
                UnityIpcServer.DefaultListenerStopTimeout);
        }

        private static UnityIpcRequestHandler CreateRequestHandler (
            ISessionTokenValidator sessionTokenValidator,
            IExecuteRequestDispatcher executeRequestDispatcher,
            IUnityTestRunService testRunService,
            IUnityShutdownAdmissionCoordinator shutdownAdmissionCoordinator)
        {
            var daemonLogStream = new DaemonLogRingBuffer();
            daemonLogStream.Write("ipc", "info", "server booted");
            var unityLogStream = new UnityLogRingBuffer();
            unityLogStream.Write(IpcUnityLogsSourceCodec.Runtime, IpcDaemonLogsLevelCodec.Info, "runtime booted", "at Bootstrap.Start()");
            var requestExecutor = new InlineMainThreadRequestExecutor();
            var methodDispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[]
                {
                    new PingUnityIpcMethodHandler(
                        new AssemblyServerVersionProvider(),
                        new StubUnityEditorReadinessGate(),
                        "project-fingerprint",
                        NoOpDaemonLogger.Instance),
                    new ExecuteUnityIpcMethodHandler(
                        executeRequestDispatcher,
                        new IpcRequestTimeoutScopeFactory(),
                        IpcProjectIdentity.Unknown),
                    new TestRunUnityIpcMethodHandler(testRunService, new IpcRequestTimeoutScopeFactory()),
                    new DaemonLogsReadUnityIpcMethodHandler(
                        daemonLogStream,
                        new DaemonLogsReadRequestValidator(),
                        new DaemonLogsReadQueryEngine(),
                        new DaemonLogsReadResponseFactory(),
                        NoOpDaemonLogger.Instance),
                    new UnityLogsReadUnityIpcMethodHandler(
                        unityLogStream,
                        new UnityLogsReadRequestValidator(),
                        new UnityLogsReadQueryEngine(),
                        new UnityLogsReadResponseFactory(),
                        NoOpDaemonLogger.Instance),
                    new UnityConsoleClearUnityIpcMethodHandler(
                        new StubUnityConsoleClearer(),
                        new StubUnityEditorReadinessGate(DaemonEditorMode.Gui),
                        NoOpDaemonLogger.Instance),
                    new ShutdownUnityIpcMethodHandler(
                        NoOpDaemonLogger.Instance,
                        shutdownAdmissionCoordinator),
                },
                requestExecutor,
                requestExecutor,
                recoverableOperationStore: null,
                daemonLogger: NoOpDaemonLogger.Instance);
            return new UnityIpcRequestHandler(
                sessionTokenValidator,
                methodDispatcher,
                NoOpDaemonLogger.Instance);
        }

        private sealed class RecordingTransportHandle : IDisposable
        {
            private int disposeCallCount;

            public int DisposeCallCount => Volatile.Read(ref disposeCallCount);

            public void Dispose ()
            {
                Interlocked.Increment(ref disposeCallCount);
            }
        }

        private sealed class BlockingDisposeTransportHandle : IDisposable
        {
            private readonly TaskCompletionSource<bool> disposeStarted =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly ManualResetEventSlim allowDispose = new ManualResetEventSlim();

            private int disposeCallCount;

            private int disposeCompleted;

            public Task DisposeStarted => disposeStarted.Task;

            public int DisposeCallCount => Volatile.Read(ref disposeCallCount);

            public bool DisposeCompleted => Volatile.Read(ref disposeCompleted) != 0;

            public void AllowDispose ()
            {
                allowDispose.Set();
            }

            public void Dispose ()
            {
                Interlocked.Increment(ref disposeCallCount);
                disposeStarted.TrySetResult(true);
                allowDispose.Wait();
                Volatile.Write(ref disposeCompleted, 1);
            }
        }

        private sealed class TestShutdownAdmissionCoordinator : IUnityShutdownAdmissionCoordinator
        {
            private IpcRequest preparedRequest;

            public bool TryPrepare (IpcRequest request, out string errorMessage)
            {
                preparedRequest = request;
                errorMessage = null;
                return true;
            }

            public bool TryCommit (IpcRequest request)
            {
                return ReferenceEquals(preparedRequest, request);
            }

            public void Abort (IpcRequest request)
            {
                if (ReferenceEquals(preparedRequest, request))
                {
                    preparedRequest = null;
                }
            }
        }

        private static async Task<string> ReadUnixFileModeAsync (string path)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/stat",
                Arguments = CreateStatArguments(path),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(startInfo);
            Assert.That(process, Is.Not.Null);
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await TestAwaiter.WaitAsync(WaitForExitAsync(process), "stat process exit", SignalWaitTimeout);
            var output = await TestAwaiter.WaitAsync(outputTask, "stat stdout read", SignalWaitTimeout);
            var error = await TestAwaiter.WaitAsync(errorTask, "stat stderr read", SignalWaitTimeout);
            Assert.That(process.ExitCode, Is.EqualTo(0), error);
            return NormalizeUnixFileMode(output);
        }

        private static Task WaitForExitAsync (Process process)
        {
            if (process.HasExited)
            {
                return Task.CompletedTask;
            }

            var taskCompletionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnExited (object sender, EventArgs args)
            {
                process.Exited -= OnExited;
                taskCompletionSource.TrySetResult(null);
            }

            process.EnableRaisingEvents = true;
            process.Exited += OnExited;

            if (process.HasExited)
            {
                process.Exited -= OnExited;
                return Task.CompletedTask;
            }

            return taskCompletionSource.Task;
        }

        private static string CreateStatArguments (string path)
        {
            var escapedPath = path.Replace("\"", "\\\"");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return string.Concat("-f %Mp%Lp \"", escapedPath, "\"");
            }

            return string.Concat("-c %a \"", escapedPath, "\"");
        }

        private static string NormalizeUnixFileMode (string output)
        {
            var trimmedOutput = output.Trim();
            if (trimmedOutput.Length == 3)
            {
                return string.Concat("0", trimmedOutput);
            }

            return trimmedOutput;
        }

        private static string CreateSocketPathWithByteLength (int totalBytes)
        {
            var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var basePath = Path.Combine(tempRoot, "ucli-test-", UcliIpcEndpointNames.UnixSocketFileName);
            var additionalBytes = totalBytes - Encoding.UTF8.GetByteCount(basePath);
            Assert.That(additionalBytes, Is.GreaterThanOrEqualTo(0));
            return Path.Combine(
                tempRoot,
                "ucli-test-" + new string('a', additionalBytes),
                UcliIpcEndpointNames.UnixSocketFileName);
        }

        private sealed class StubDaemonShutdownSignal : IDaemonShutdownSignal
        {
            private readonly TaskCompletionSource<bool> signalObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public int SignalCount { get; private set; }

            public bool IsSignaled => SignalCount > 0;

            public Task SignalObserved => signalObserved.Task;

            public void Signal ()
            {
                SignalCount++;
                signalObserved.TrySetResult(true);
            }

            public Task WaitAsync (CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsSignaled)
                {
                    return Task.CompletedTask;
                }

                return signalObserved.Task;
            }
        }

        private sealed class ShutdownResultConnectionHandler : IUnityIpcConnectionHandler
        {
            public int CallCount { get; private set; }

            public Task<UnityIpcConnectionHandleResult> HandleAsync (
                Stream stream,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                var request = CreateShutdownRequest(CanonicalSessionToken, Guid.NewGuid());
                var response = new IpcResponse(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: request.RequestId,
                    status: IpcProtocol.StatusOk,
                    payload: JsonSerializer.SerializeToElement(new IpcShutdownResponse(true, "ok"), SerializerOptions),
                    errors: Array.Empty<IpcError>());
                return Task.FromResult(new UnityIpcConnectionHandleResult(
                    request,
                    response,
                    isShutdownAdmissionCommitted: true));
            }
        }

        private sealed class BlockingConnectionHandler : IUnityIpcConnectionHandler
        {
            private readonly object syncRoot = new object();

            private readonly TaskCompletionSource<bool> firstConnectionObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> secondConnectionObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int callCount;

            public int CallCount
            {
                get
                {
                    lock (syncRoot)
                    {
                        return callCount;
                    }
                }
            }

            public Task FirstConnectionObserved => firstConnectionObserved.Task;

            public Task SecondConnectionObserved => secondConnectionObserved.Task;

            public Task<UnityIpcConnectionHandleResult> HandleAsync (
                Stream stream,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int currentCallCount;
                lock (syncRoot)
                {
                    callCount++;
                    currentCallCount = callCount;
                }

                if (currentCallCount == 1)
                {
                    firstConnectionObserved.TrySetResult(true);
                }
                else if (currentCallCount == 2)
                {
                    secondConnectionObserved.TrySetResult(true);
                }

                return CompleteAfterReleaseAsync(cancellationToken);
            }

            public void Release ()
            {
                release.TrySetResult(true);
            }

            private async Task<UnityIpcConnectionHandleResult> CompleteAfterReleaseAsync (CancellationToken cancellationToken)
            {
                using var cancellationRegistration = cancellationToken.Register(() =>
                {
                    release.TrySetCanceled(cancellationToken);
                });
                await release.Task;
                cancellationToken.ThrowIfCancellationRequested();
                return default;
            }
        }

        private sealed class StubConnectionHandler : IUnityIpcConnectionHandler
        {
            public Task<UnityIpcConnectionHandleResult> HandleAsync (
                Stream stream,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(default(UnityIpcConnectionHandleResult));
            }
        }

        private sealed class InlineMainThreadRequestExecutor :
            IUnityMainThreadRequestExecutor,
            IUnityControlPlaneRequestExecutor
        {
            public Task<T> ExecuteAsync<T> (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (workItem == null)
                {
                    throw new ArgumentNullException(nameof(workItem));
                }

                return workItem();
            }
        }

        private sealed class StubSessionTokenValidator : ISessionTokenValidator
        {
            private readonly bool accepted;

            public StubSessionTokenValidator (bool accepted)
            {
                this.accepted = accepted;
            }

            public Task<bool> ValidateAsync (
                string sessionToken,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(accepted);
            }
        }

        private sealed class ThrowingSessionTokenValidator : ISessionTokenValidator
        {
            private readonly Exception exception;

            public ThrowingSessionTokenValidator (Exception exception)
            {
                this.exception = exception;
            }

            public Task<bool> ValidateAsync (
                string sessionToken,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw exception;
            }
        }

        private sealed class StubExecuteRequestDispatcher : IExecuteRequestDispatcher
        {
            public int CallCount { get; private set; }

            public ExecuteDispatchContext LastContext { get; private set; }

            public Task<IpcResponse> DispatchAsync (
                IpcExecuteRequest request,
                ExecuteDispatchContext context,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                LastContext = context;

                var payload = JsonSerializer.SerializeToElement(
                    new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>()),
                    SerializerOptions);
                return Task.FromResult(new IpcResponse(
                    protocolVersion: context.ProtocolVersion,
                    requestId: context.RequestId,
                    status: IpcProtocol.StatusOk,
                    payload: payload,
                    errors: Array.Empty<IpcError>()));
            }
        }

        private sealed class StubUnityTestRunService : IUnityTestRunService
        {
            private readonly UnityTestRunServiceResult response;

            public StubUnityTestRunService ()
                : this(UnityTestRunServiceResult.Success(new IpcTestRunResponse(0)))
            {
            }

            public StubUnityTestRunService (IpcTestRunResponse response)
                : this(UnityTestRunServiceResult.Success(response))
            {
            }

            public StubUnityTestRunService (UnityTestRunServiceResult response)
            {
                this.response = response ?? throw new ArgumentNullException(nameof(response));
            }

            public int CallCount { get; private set; }

            public IpcTestRunRequest LastRequest { get; private set; }

            public Task<UnityTestRunServiceResult> ExecuteAsync (
                IpcTestRunRequest request,
                IUnityTestRunProgressSink progressSink = null,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                LastRequest = request;
                return Task.FromResult(response);
            }
        }

        private sealed class StubUnityConsoleClearer : IUnityConsoleClearer
        {
            public UnityConsoleClearResult Clear ()
            {
                return UnityConsoleClearResult.Success();
            }
        }

        private sealed class ThrowingTransportListener : IUnityIpcTransportListener
        {
            private readonly string message;

            public ThrowingTransportListener (
                IpcTransportKind transportKind,
                string message)
            {
                TransportKind = transportKind;
                this.message = message;
            }

            public IpcTransportKind TransportKind { get; }

            public Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                return Task.FromException(new InvalidOperationException(message));
            }

            public void Release ()
            {
            }
        }

        private sealed class DelayedThrowingTransportListener : IUnityIpcTransportListener
        {
            private readonly string message;

            private readonly TaskCompletionSource<bool> runEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> faultRelease =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public DelayedThrowingTransportListener (
                IpcTransportKind transportKind,
                string message)
            {
                TransportKind = transportKind;
                this.message = message;
            }

            public IpcTransportKind TransportKind { get; }

            public Task RunEntered => runEntered.Task;

            public void ReleaseFault ()
            {
                faultRelease.TrySetResult(true);
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                runEntered.TrySetResult(true);
                await faultRelease.Task;
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException(message);
            }

            public void Release ()
            {
            }
        }

        private sealed class StartedThenThrowingTransportListener : IUnityIpcTransportListener
        {
            private readonly string message;

            private readonly TaskCompletionSource<bool> faultRelease =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public StartedThenThrowingTransportListener (
                IpcTransportKind transportKind,
                string message)
            {
                TransportKind = transportKind;
                this.message = message;
            }

            public IpcTransportKind TransportKind { get; }

            public void ReleaseFault ()
            {
                faultRelease.TrySetResult(true);
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                onStarted();
                await faultRelease.Task;
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException(message);
            }

            public void Release ()
            {
            }
        }

        private sealed class CompletionReportingTransportListener : IUnityIpcTransportListener
        {
            private readonly UnityIpcConnectionHandleResult result;

            private readonly TaskCompletionSource<bool> cancellationObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public CompletionReportingTransportListener (
                IpcTransportKind transportKind,
                UnityIpcConnectionHandleResult result)
            {
                TransportKind = transportKind;
                this.result = result;
            }

            public IpcTransportKind TransportKind { get; }

            public int ConnectionCompletedCallCount { get; private set; }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                onStarted();
                ConnectionCompletedCallCount++;
                onConnectionCompleted(result);

                using var cancellationRegistration = cancellationToken.Register(() =>
                {
                    cancellationObserved.TrySetCanceled(cancellationToken);
                });
                await cancellationObserved.Task;
            }

            public void Release ()
            {
            }
        }

        private sealed class BlockingTransportListener : IUnityIpcTransportListener
        {
            private readonly bool signalStarted;

            private readonly TaskCompletionSource<bool> runEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> cancellationObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public BlockingTransportListener (
                IpcTransportKind transportKind,
                bool signalStarted = false)
            {
                TransportKind = transportKind;
                this.signalStarted = signalStarted;
            }

            public IpcTransportKind TransportKind { get; }

            public Task RunEntered => runEntered.Task;

            public Task CancellationObserved => cancellationObserved.Task;

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                runEntered.TrySetResult(true);
                if (signalStarted)
                {
                    onStarted();
                }

                var waitSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var cancellationRegistration = cancellationToken.Register(() =>
                {
                    cancellationObserved.TrySetResult(true);
                    waitSource.TrySetCanceled(cancellationToken);
                });

                await waitSource.Task;
            }

            public void Release ()
            {
            }
        }

        private sealed class NonCompletingTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> runEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> complete =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public NonCompletingTransportListener (IpcTransportKind transportKind)
            {
                TransportKind = transportKind;
            }

            public IpcTransportKind TransportKind { get; }

            public int ReleaseCallCount { get; private set; }

            public Task RunEntered => runEntered.Task;

            public void Complete ()
            {
                complete.TrySetResult(true);
            }

            public async Task RunAsync (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                Action<UnityIpcConnectionHandleResult> onConnectionCompleted,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                runEntered.TrySetResult(true);
                onStarted();
                await complete.Task;
                cancellationToken.ThrowIfCancellationRequested();
            }

            public void Release ()
            {
                ReleaseCallCount++;
            }
        }

        private sealed class RecordingSynchronizationContext : SynchronizationContext
        {
            private int postCallCount;

            public int PostCallCount => postCallCount;

            public override void Post (
                SendOrPostCallback d,
                object state)
            {
                Interlocked.Increment(ref postCallCount);
                ThreadPool.QueueUserWorkItem(_ => d(state));
            }
        }

        private sealed class ManuallyPumpedSynchronizationContext : SynchronizationContext
        {
            private readonly object syncRoot = new object();

            private readonly Queue<(SendOrPostCallback Callback, object State)> callbacks =
                new Queue<(SendOrPostCallback Callback, object State)>();

            private readonly ManualResetEventSlim callbackAvailable = new ManualResetEventSlim();

            public override void Post (
                SendOrPostCallback d,
                object state)
            {
                lock (syncRoot)
                {
                    callbacks.Enqueue((d, state));
                    callbackAvailable.Set();
                }
            }

            public bool WaitForCallback (TimeSpan timeout)
            {
                return callbackAvailable.Wait(timeout);
            }

            public void ExecuteOldestCallback ()
            {
                (SendOrPostCallback Callback, object State) callback;
                lock (syncRoot)
                {
                    callback = callbacks.Dequeue();
                    if (callbacks.Count == 0)
                    {
                        callbackAvailable.Reset();
                    }
                }

                var originalSynchronizationContext = Current;
                try
                {
                    SetSynchronizationContext(this);
                    callback.Callback(callback.State);
                }
                finally
                {
                    SetSynchronizationContext(originalSynchronizationContext);
                }
            }
        }
    }
}
