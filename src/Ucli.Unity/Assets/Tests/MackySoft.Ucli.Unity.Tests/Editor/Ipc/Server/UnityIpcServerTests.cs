using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartupCoordinator_Wait_WhenStartupCompletesBeforeCancellation_DoesNotThrow () => UniTask.ToCoroutine(async () =>
        {
            var startupCoordinator = new UnityIpcServerStartupCoordinator();
            using var cancellationTokenSource = new CancellationTokenSource();
            var waitTask = startupCoordinator.WaitAsync(cancellationTokenSource.Token);

            startupCoordinator.Complete();
            cancellationTokenSource.Cancel();

            await TestAwaiter.WaitAsync(waitTask, "Startup completion before cancellation", SignalWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartupCoordinator_Wait_WhenStartupCompletesShortlyAfterCancellation_DoesNotThrow () => UniTask.ToCoroutine(async () =>
        {
            var startupCoordinator = new UnityIpcServerStartupCoordinator();
            using var cancellationTokenSource = new CancellationTokenSource();
            using var completionRegistration = cancellationTokenSource.Token.Register(startupCoordinator.Complete);
            var waitTask = startupCoordinator.WaitAsync(cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            await TestAwaiter.WaitAsync(waitTask, "Startup completion after cancellation", SignalWaitTimeout);
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

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenAddressIsWhitespace_ThrowsArgumentException () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForLifecycle();
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, " ");
            var exception = await AsyncExceptionCapture.CaptureAsync<ArgumentException>(async () =>
            {
                await server.StartAsync(endpoint).AsUniTask();
            }, "Whitespace endpoint start", SignalWaitTimeout);

            Assert.That(exception.ParamName, Is.EqualTo("endpoint"));
        });

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
            var listener = new UnixDomainSocketUnityIpcTransportListener();
            var startedTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationTokenSource = new CancellationTokenSource();

            var runTask = listener.RunAsync(
                address,
                new StubConnectionHandler(),
                () => startedTaskSource.TrySetResult(true),
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
        public IEnumerator UnixDomainSocketListener_Run_WhenAddressExceedsSupportedByteLength_ThrowsArgumentException () => UniTask.ToCoroutine(async () =>
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return;
            }

            var address = CreateSocketPathWithByteLength(IpcTransportConstraints.UnixDomainSocketPathMaxBytes + 1);
            var listener = new UnixDomainSocketUnityIpcTransportListener();
            var exception = await AsyncExceptionCapture.CaptureAsync<ArgumentException>(async () =>
            {
                await listener.RunAsync(
                        address,
                        new StubConnectionHandler(),
                        () => { },
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
        public IEnumerator HandleRequest_WhenSessionTokenIsMissing_ReturnsSessionTokenRequiredError () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal());
            var request = CreatePingRequest(sessionToken: string.Empty);

            var response = await server.HandleRequestAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcSessionErrorCodes.SessionTokenRequired));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleRequest_WhenSessionTokenIsInvalid_ReturnsSessionTokenInvalidError () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: false),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal());
            var request = CreatePingRequest(sessionToken: "invalid-token");

            var response = await server.HandleRequestAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcSessionErrorCodes.SessionTokenInvalid));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleRequest_WhenSessionTokenValidationThrows_ReturnsInternalError () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForRequestHandling(
                new ThrowingSessionTokenValidator(new IOException("session file read failed")),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal());
            var request = CreatePingRequest(sessionToken: "valid-token");

            var response = await server.HandleRequestAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleRequest_WhenValidTokenAndPing_ReturnsPingResponse () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal());
            var request = CreatePingRequest(sessionToken: "valid-token");

            var response = await server.HandleRequestAsync(request);

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
        public IEnumerator HandleRequest_WhenValidTokenAndExecute_CallsDispatcher () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = new StubExecuteRequestDispatcher();
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                dispatcher,
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal());
            var request = CreateExecuteRequest(sessionToken: "valid-token", requestId: "req-execute");

            var response = await server.HandleRequestAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(dispatcher.CallCount, Is.EqualTo(1));
            Assert.That(dispatcher.LastContext, Is.Not.Null);
            Assert.That(dispatcher.LastContext.RequestId, Is.EqualTo("req-execute"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleRequest_WhenShutdownAccepted_SignalsShutdown () => UniTask.ToCoroutine(async () =>
        {
            var shutdownSignal = new StubDaemonShutdownSignal();
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                shutdownSignal);
            var request = CreateShutdownRequest(sessionToken: "valid-token", requestId: "req-shutdown");

            var response = await server.HandleRequestAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            var payload = response.Payload.Deserialize<IpcShutdownResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Accepted, Is.True);
            Assert.That(shutdownSignal.SignalCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleRequest_WhenValidTokenAndTestRun_CallsTestRunService () => UniTask.ToCoroutine(async () =>
        {
            var testRunService = new StubUnityTestRunService(new IpcTestRunResponse(2));
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                testRunService,
                new StubDaemonShutdownSignal());
            var request = CreateTestRunRequest(sessionToken: "valid-token", requestId: "req-test-run", failFast: true);

            var response = await server.HandleRequestAsync(request);

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
        public IEnumerator HandleRequest_WhenTestRunServiceReturnsLifecycleFailure_PreservesErrorCode () => UniTask.ToCoroutine(async () =>
        {
            var testRunService = new StubUnityTestRunService(UnityTestRunServiceResult.Failure(
                new IpcError(EditorLifecycleErrorCodes.EditorBusy, "Unity editor is busy with internal work.", null)));
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                testRunService,
                new StubDaemonShutdownSignal());
            var request = CreateTestRunRequest(sessionToken: "valid-token", requestId: "req-test-run-lifecycle-error");

            var response = await server.HandleRequestAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
            Assert.That(response.Errors[0].Message, Is.EqualTo("Unity editor is busy with internal work."));
            Assert.That(testRunService.CallCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleRequest_WhenTestRunPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal());
            var invalidPayload = JsonSerializer.SerializeToElement(123, SerializerOptions);
            var request = new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "req-test-run-invalid",
                SessionToken: "valid-token",
                Method: IpcMethodNames.TestRun,
                Payload: invalidPayload);

            var response = await server.HandleRequestAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleRequest_WhenValidTokenAndDaemonLogsRead_ReturnsDaemonLogEvents () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal());
            var request = CreateDaemonLogsReadRequest(sessionToken: "valid-token", requestId: "req-daemon-logs");

            var response = await server.HandleRequestAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            var payload = response.Payload.Deserialize<IpcDaemonLogsReadResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Events.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(payload.NextCursor, Is.Not.Empty);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleRequest_WhenValidTokenAndUnityLogsRead_ReturnsUnityLogEvents () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal());
            var request = CreateUnityLogsReadRequest(sessionToken: "valid-token", requestId: "req-unity-logs");

            var response = await server.HandleRequestAsync(request);

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
        public IEnumerator HandleRequest_WhenValidTokenAndUnityConsoleClear_ReturnsSuccessResponse () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal());
            var request = CreateUnityConsoleClearRequest(sessionToken: "valid-token", requestId: "req-unity-console-clear");

            var response = await server.HandleRequestAsync(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            var payload = response.Payload.Deserialize<IpcUnityConsoleClearResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
        });

        private static IpcRequest CreatePingRequest (string sessionToken)
        {
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "req-ping",
                SessionToken: sessionToken,
                Method: IpcMethodNames.Ping,
                Payload: JsonSerializer.SerializeToElement(new IpcPingRequest("tests"), SerializerOptions));
        }

        private static IpcRequest CreateExecuteRequest (
            string sessionToken,
            string requestId)
        {
            var arguments = JsonSerializer.SerializeToElement(
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId,
                    ops = Array.Empty<object>(),
                },
                SerializerOptions);
            var payload = JsonSerializer.SerializeToElement(
                new IpcExecuteRequest(UcliCommandIds.Validate, arguments),
                SerializerOptions);
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: sessionToken,
                Method: IpcMethodNames.Execute,
                Payload: payload);
        }

        private static IpcRequest CreateShutdownRequest (
            string sessionToken,
            string requestId)
        {
            var payload = JsonSerializer.SerializeToElement(
                new IpcShutdownRequest("tests"),
                SerializerOptions);
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: sessionToken,
                Method: IpcMethodNames.Shutdown,
                Payload: payload);
        }

        private static IpcRequest CreateTestRunRequest (
            string sessionToken,
            string requestId,
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
                    FailFast: failFast),
                SerializerOptions);
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: sessionToken,
                Method: IpcMethodNames.TestRun,
                Payload: payload);
        }

        private static IpcRequest CreateDaemonLogsReadRequest (
            string sessionToken,
            string requestId)
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
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: sessionToken,
                Method: IpcMethodNames.DaemonLogsRead,
                Payload: payload);
        }

        private static IpcRequest CreateUnityLogsReadRequest (
            string sessionToken,
            string requestId)
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
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: sessionToken,
                Method: IpcMethodNames.UnityLogsRead,
                Payload: payload);
        }

        private static IpcRequest CreateUnityConsoleClearRequest (
            string sessionToken,
            string requestId)
        {
            var payload = JsonSerializer.SerializeToElement(
                new IpcUnityConsoleClearRequest("tests"),
                SerializerOptions);
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: sessionToken,
                Method: IpcMethodNames.UnityConsoleClear,
                Payload: payload);
        }

        private static UnityIpcServer CreateServerForLifecycle ()
        {
            return CreateServer(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal(),
                new IUnityIpcTransportListener[]
                {
                    new NamedPipeUnityIpcTransportListener(),
                    new UnixDomainSocketUnityIpcTransportListener(),
                });
        }

        private static UnityIpcServer CreateServerForRequestHandling (
            ISessionTokenValidator sessionTokenValidator,
            IExecuteRequestDispatcher executeRequestDispatcher,
            IUnityTestRunService testRunService,
            IDaemonShutdownSignal shutdownSignal)
        {
            return CreateServer(
                sessionTokenValidator,
                executeRequestDispatcher,
                testRunService,
                shutdownSignal,
                Array.Empty<IUnityIpcTransportListener>());
        }

        private static UnityIpcServer CreateServer (
            ISessionTokenValidator sessionTokenValidator,
            IExecuteRequestDispatcher executeRequestDispatcher,
            IUnityTestRunService testRunService,
            IDaemonShutdownSignal shutdownSignal,
            IReadOnlyList<IUnityIpcTransportListener> transportListeners)
        {
            var daemonLogStream = new DaemonLogRingBuffer();
            daemonLogStream.Write("ipc", "info", "server booted");
            var unityLogStream = new UnityLogRingBuffer();
            unityLogStream.Write(IpcUnityLogsSourceCodec.Runtime, IpcDaemonLogsLevelCodec.Info, "runtime booted", "at Bootstrap.Start()");
            var methodDispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[]
                {
                    new PingUnityIpcMethodHandler(new AssemblyServerVersionProvider(), new StubUnityEditorReadinessGate(), "project-fingerprint"),
                    new ExecuteUnityIpcMethodHandler(executeRequestDispatcher),
                    new TestRunUnityIpcMethodHandler(testRunService),
                    new DaemonLogsReadUnityIpcMethodHandler(
                        daemonLogStream,
                        new DaemonLogsReadRequestValidator(),
                        new DaemonLogsReadQueryEngine(),
                        new DaemonLogsReadResponseFactory()),
                    new UnityLogsReadUnityIpcMethodHandler(
                        unityLogStream,
                        new UnityLogsReadRequestValidator(),
                        new UnityLogsReadQueryEngine(),
                        new UnityLogsReadResponseFactory()),
                    new UnityConsoleClearUnityIpcMethodHandler(
                        new StubUnityConsoleClearer(),
                        new StubUnityEditorReadinessGate(DaemonEditorMode.Gui)),
                    new ShutdownUnityIpcMethodHandler(),
                });
            var requestHandler = new UnityIpcRequestHandler(sessionTokenValidator, methodDispatcher);
            var requestProcessor = new UnityIpcRequestProcessor(
                requestHandler,
                new InlineMainThreadRequestExecutor());
            var connectionHandler = new UnityIpcConnectionHandler(
                requestProcessor,
                shutdownSignal);
            return new UnityIpcServer(requestProcessor, connectionHandler, transportListeners);
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
            public int SignalCount { get; private set; }

            public void Signal ()
            {
                SignalCount++;
            }

            public Task WaitAsync (CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
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

        private sealed class InlineMainThreadRequestExecutor : IUnityMainThreadRequestExecutor
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
                    ProtocolVersion: context.ProtocolVersion,
                    RequestId: context.RequestId,
                    Status: IpcProtocol.StatusOk,
                    Payload: payload,
                    Errors: Array.Empty<IpcError>()));
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
    }
}
