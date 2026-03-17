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
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
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

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartupCoordinator_Wait_WhenStartupCompletesBeforeCancellation_DoesNotThrow () => UniTask.ToCoroutine(async () =>
        {
            var startupCoordinator = new UnityIpcServerStartupCoordinator();
            using var cancellationTokenSource = new CancellationTokenSource();
            var waitTask = startupCoordinator.Wait(cancellationTokenSource.Token);

            startupCoordinator.Complete();
            cancellationTokenSource.Cancel();

            await waitTask;
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartupCoordinator_Wait_WhenStartupCompletesShortlyAfterCancellation_DoesNotThrow () => UniTask.ToCoroutine(async () =>
        {
            var startupCoordinator = new UnityIpcServerStartupCoordinator();
            using var cancellationTokenSource = new CancellationTokenSource();
            var waitTask = startupCoordinator.Wait(cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();
            await UniTask.Yield();
            startupCoordinator.Complete();

            await waitTask;
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartupCoordinator_Wait_WhenCanceledWithoutStartup_ThrowsOperationCanceledException () => UniTask.ToCoroutine(async () =>
        {
            var startupCoordinator = new UnityIpcServerStartupCoordinator();
            using var cancellationTokenSource = new CancellationTokenSource();
            var waitTask = startupCoordinator.Wait(cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await waitTask.AsUniTask();
            });
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenEndpointIsNull_ThrowsArgumentNullException () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForLifecycle();
            var exception = await AsyncExceptionCapture.CaptureAsync<ArgumentNullException>(async () =>
            {
                await server.Start(null).AsUniTask();
            });

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
                await server.Start(endpoint).AsUniTask();
            });

            Assert.That(exception.ParamName, Is.EqualTo("endpoint"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_ThenStop_TransitionsRunningState () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForLifecycle();
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test");
            await server.Start(endpoint).AsUniTask();
            Assert.That(server.IsRunning, Is.True);

            await server.Stop().AsUniTask();
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

            var runTask = listener.Run(
                address,
                new StubConnectionHandler(),
                () => startedTaskSource.TrySetResult(true),
                cancellationTokenSource.Token);

            try
            {
                await startedTaskSource.Task;

                Assert.That(Directory.Exists(socketDirectoryPath), Is.True);
                Assert.That(File.Exists(address), Is.True);
                Assert.That(await ReadUnixFileMode(socketDirectoryPath), Is.EqualTo("0700"));
                Assert.That(await ReadUnixFileMode(address), Is.EqualTo("0600"));
            }
            finally
            {
                cancellationTokenSource.Cancel();
                listener.Release();

                try
                {
                    await runTask;
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
                await listener.Run(
                        address,
                        new StubConnectionHandler(),
                        () => { },
                        CancellationToken.None)
                    .AsUniTask();
            });

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
                await server.Stop(cancellationTokenSource.Token).AsUniTask();
            });
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
                await server.Start(endpoint).AsUniTask();
            });

            Assert.That(server.IsRunning, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenListenerThrowsAfterDelayBeforeStartupSignal_ThrowsAndResetsRunningState () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServer(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal(),
                new IUnityIpcTransportListener[]
                {
                    new DelayedThrowingTransportListener(
                        IpcTransportKind.NamedPipe,
                        "listener failed after delay",
                        TimeSpan.FromMilliseconds(50)),
                });
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test-delayed-failure");

            await AsyncExceptionCapture.CaptureAsync<InvalidOperationException>(async () =>
            {
                await server.Start(endpoint).AsUniTask();
            });

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

            var startTask = server.Start(endpoint, cancellationTokenSource.Token);
            await blockingListener.RunEntered;
            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await startTask.AsUniTask();
            });

            await blockingListener.CancellationObserved;
            Assert.That(server.IsRunning, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator WaitForTermination_WhenListenerFaultsAfterStartupSignal_Throws () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServer(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                new StubUnityTestRunService(),
                new StubDaemonShutdownSignal(),
                new IUnityIpcTransportListener[]
                {
                    new StartedThenThrowingTransportListener(
                        IpcTransportKind.NamedPipe,
                        "listener failed after startup",
                        TimeSpan.FromMilliseconds(50)),
                });
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test-fault-after-startup");

            await server.Start(endpoint).AsUniTask();
            await AsyncExceptionCapture.CaptureAsync<InvalidOperationException>(async () =>
            {
                await server.WaitForTermination(CancellationToken.None).AsUniTask();
            });

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

            var response = await server.HandleRequest(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.SessionTokenRequired));
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

            var response = await server.HandleRequest(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.SessionTokenInvalid));
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

            var response = await server.HandleRequest(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InternalError));
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

            var response = await server.HandleRequest(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            var payload = response.Payload.Deserialize<IpcPingResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Runtime, Is.EqualTo("batchmode"));
            Assert.That(string.IsNullOrWhiteSpace(payload.UnityVersion), Is.False);
            Assert.That(string.IsNullOrWhiteSpace(payload.ServerVersion), Is.False);
            var expectedServerVersion = new AssemblyServerVersionProvider().GetVersion();
            Assert.That(payload.ServerVersion, Is.EqualTo(expectedServerVersion));
            Assert.That(Regex.IsMatch(payload.ServerVersion, "^[0-9]+\\.[0-9]+\\.[0-9]+(\\.[0-9]+)?$"), Is.True);
            Assert.That(
                payload.CompileState == IpcCompileStateCodec.Ready
                || payload.CompileState == IpcCompileStateCodec.Compiling,
                Is.True);
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

            var response = await server.HandleRequest(request);

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

            var response = await server.HandleRequest(request);

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
            var request = CreateTestRunRequest(sessionToken: "valid-token", requestId: "req-test-run");

            var response = await server.HandleRequest(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(testRunService.CallCount, Is.EqualTo(1));
            Assert.That(testRunService.LastRequest, Is.Not.Null);
            Assert.That(testRunService.LastRequest.TestPlatform, Is.EqualTo("editmode"));
            var payload = response.Payload.Deserialize<IpcTestRunResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.ExitCode, Is.EqualTo(2));
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

            var response = await server.HandleRequest(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
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

            var response = await server.HandleRequest(request);

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

            var response = await server.HandleRequest(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            var payload = response.Payload.Deserialize<IpcUnityLogsReadResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Events.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(payload.Events[0].Source, Is.EqualTo("runtime"));
            Assert.That(payload.NextCursor, Is.Not.Empty);
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
            string requestId)
        {
            var payload = JsonSerializer.SerializeToElement(
                new IpcTestRunRequest(
                    TestPlatform: "editmode",
                    BuildTarget: null,
                    TestFilter: null,
                    TestCategories: Array.Empty<string>(),
                    AssemblyNames: Array.Empty<string>(),
                    TestSettingsPath: null,
                    ResultsXmlPath: "/tmp/results.xml",
                    EditorLogPath: "/tmp/editor.log"),
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
                    new PingUnityIpcMethodHandler(new AssemblyServerVersionProvider()),
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

        private static async Task<string> ReadUnixFileMode (string path)
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
            await WaitForExit(process);
            var output = await outputTask;
            var error = await errorTask;
            Assert.That(process.ExitCode, Is.EqualTo(0), error);
            return NormalizeUnixFileMode(output);
        }

        private static Task WaitForExit (Process process)
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

            public Task Wait (CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        private sealed class StubConnectionHandler : IUnityIpcConnectionHandler
        {
            public Task<UnityIpcConnectionHandleResult> Handle (
                Stream stream,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(default(UnityIpcConnectionHandleResult));
            }
        }

        private sealed class InlineMainThreadRequestExecutor : IUnityMainThreadRequestExecutor
        {
            public Task<IpcResponse> Execute (
                Func<Task<IpcResponse>> requestHandler,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (requestHandler == null)
                {
                    throw new ArgumentNullException(nameof(requestHandler));
                }

                return requestHandler();
            }
        }

        private sealed class StubSessionTokenValidator : ISessionTokenValidator
        {
            private readonly bool accepted;

            public StubSessionTokenValidator (bool accepted)
            {
                this.accepted = accepted;
            }

            public Task<bool> Validate (
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

            public Task<bool> Validate (
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

            public Task<IpcResponse> Dispatch (
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
            private readonly IpcTestRunResponse response;

            public StubUnityTestRunService ()
                : this(new IpcTestRunResponse(0))
            {
            }

            public StubUnityTestRunService (IpcTestRunResponse response)
            {
                this.response = response ?? throw new ArgumentNullException(nameof(response));
            }

            public int CallCount { get; private set; }

            public IpcTestRunRequest LastRequest { get; private set; }

            public Task<IpcTestRunResponse> Execute (
                IpcTestRunRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                LastRequest = request;
                return Task.FromResult(response);
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

            public Task Run (
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

            private readonly TimeSpan delay;

            public DelayedThrowingTransportListener (
                IpcTransportKind transportKind,
                string message,
                TimeSpan delay)
            {
                TransportKind = transportKind;
                this.message = message;
                this.delay = delay;
            }

            public IpcTransportKind TransportKind { get; }

            public async Task Run (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
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

            private readonly TimeSpan delay;

            public StartedThenThrowingTransportListener (
                IpcTransportKind transportKind,
                string message,
                TimeSpan delay)
            {
                TransportKind = transportKind;
                this.message = message;
                this.delay = delay;
            }

            public IpcTransportKind TransportKind { get; }

            public async Task Run (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                onStarted();
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException(message);
            }

            public void Release ()
            {
            }
        }

        private sealed class BlockingTransportListener : IUnityIpcTransportListener
        {
            private readonly TaskCompletionSource<bool> runEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> cancellationObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public BlockingTransportListener (IpcTransportKind transportKind)
            {
                TransportKind = transportKind;
            }

            public IpcTransportKind TransportKind { get; }

            public Task RunEntered => runEntered.Task;

            public Task CancellationObserved => cancellationObserved.Task;

            public Task Run (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                Action onStarted,
                CancellationToken cancellationToken)
            {
                runEntered.TrySetResult(true);
                var waitSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var cancellationRegistration = cancellationToken.Register(() =>
                {
                    cancellationObserved.TrySetResult(true);
                    waitSource.TrySetCanceled(cancellationToken);
                });

                return waitSource.Task;
            }

            public void Release ()
            {
            }
        }
    }
}
