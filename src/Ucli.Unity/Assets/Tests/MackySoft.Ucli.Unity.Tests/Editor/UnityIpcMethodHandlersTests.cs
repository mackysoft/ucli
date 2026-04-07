using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcMethodHandlersTests
    {
        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PingHandler_WhenPayloadIsValid_ReturnsOkResponse () => UniTask.ToCoroutine(async () =>
        {
            var handler = new PingUnityIpcMethodHandler(new StubServerVersionProvider("1.2.3"), new StubUnityEditorReadinessGate());
            var request = CreatePingRequest("req-ping-valid", new IpcPingRequest("client"));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPingResponse payload, out _), Is.True);
            Assert.That(payload.ServerVersion, Is.EqualTo("1.2.3"));
            Assert.That(payload.Runtime, Is.EqualTo("batchmode"));
            Assert.That(payload.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(payload.BlockingReason, Is.Null);
            Assert.That(payload.CompileGeneration, Is.EqualTo("1"));
            Assert.That(payload.DomainReloadGeneration, Is.EqualTo("1"));
            Assert.That(payload.CanAcceptExecutionRequests, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PingHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = new PingUnityIpcMethodHandler(new StubServerVersionProvider("1.2.3"), new StubUnityEditorReadinessGate());
            var request = CreatePingRequest("req-ping-invalid", 123);

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PingHandler_WhenStartupIsPending_DoesNotConsumeStarting () => UniTask.ToCoroutine(async () =>
        {
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 0,
                domainReloadGeneration: 1,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true);
            var handler = new PingUnityIpcMethodHandler(
                new StubServerVersionProvider("1.2.3"),
                new UnityEditorReadinessGate(
                    telemetryState,
                    static () => false,
                    static () => false));

            var firstResponse = await handler.Handle(CreatePingRequest("req-ping-starting-1", new IpcPingRequest("client")), CancellationToken.None);
            var secondResponse = await handler.Handle(CreatePingRequest("req-ping-starting-2", new IpcPingRequest("client")), CancellationToken.None);

            Assert.That(IpcPayloadCodec.TryDeserialize(firstResponse.Payload, out IpcPingResponse firstPayload, out _), Is.True);
            Assert.That(IpcPayloadCodec.TryDeserialize(secondResponse.Payload, out IpcPingResponse secondPayload, out _), Is.True);
            Assert.That(firstPayload.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(secondPayload.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));

            telemetryState.ObserveEditorUpdate(
                isCompiling: false,
                isUpdating: false);
            var readyResponse = await handler.Handle(CreatePingRequest("req-ping-starting-3", new IpcPingRequest("client")), CancellationToken.None);

            Assert.That(IpcPayloadCodec.TryDeserialize(readyResponse.Payload, out IpcPingResponse readyPayload, out _), Is.True);
            Assert.That(readyPayload.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(readyPayload.CanAcceptExecutionRequests, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteHandler_WhenPayloadIsValid_CallsDispatcher () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = new StubExecuteRequestDispatcher();
            var handler = new ExecuteUnityIpcMethodHandler(dispatcher);
            var request = CreateExecuteRequest(
                "req-execute-valid",
                new IpcExecuteRequest(
                    UcliCommandIds.Validate,
                    IpcPayloadCodec.SerializeToElement(new
                    {
                        protocolVersion = IpcProtocol.CurrentVersion,
                        requestId = "req-execute-valid",
                        ops = Array.Empty<object>(),
                    })));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(dispatcher.CallCount, Is.EqualTo(1));
            Assert.That(dispatcher.LastContext, Is.Not.Null);
            Assert.That(dispatcher.LastContext.RequestId, Is.EqualTo("req-execute-valid"));
            Assert.That(dispatcher.LastRequest, Is.Not.Null);
            Assert.That(dispatcher.LastRequest.Command, Is.EqualTo(UcliCommandIds.Validate.Name));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = new ExecuteUnityIpcMethodHandler(new StubExecuteRequestDispatcher());
            var request = CreateExecuteRequest("req-execute-invalid", 123);

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenServiceSucceeds_ReturnsOkResponse () => UniTask.ToCoroutine(async () =>
        {
            var service = new StubUnityTestRunService(request => Task.FromResult(UnityTestRunServiceResult.Success(new IpcTestRunResponse(2))));
            var handler = new TestRunUnityIpcMethodHandler(service);
            var request = CreateTestRunRequest(
                "req-test-run-success",
                CreateValidTestRunPayload(failFast: true));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(service.CallCount, Is.EqualTo(1));
            Assert.That(service.LastRequest, Is.Not.Null);
            Assert.That(service.LastRequest.FailFast, Is.True);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcTestRunResponse payload, out _), Is.True);
            Assert.That(payload.ExitCode, Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenServiceReturnsLifecycleFailure_PreservesErrorCode () => UniTask.ToCoroutine(async () =>
        {
            var service = new StubUnityTestRunService(_ => Task.FromResult(UnityTestRunServiceResult.Failure(
                new IpcError(IpcErrorCodes.EditorBusy, "Unity editor is busy with internal work.", null))));
            var handler = new TestRunUnityIpcMethodHandler(service);
            var request = CreateTestRunRequest(
                "req-test-run-lifecycle-error",
                CreateValidTestRunPayload());

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.EditorBusy));
            Assert.That(response.Errors[0].Message, Is.EqualTo("Unity editor is busy with internal work."));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenServiceThrowsArgumentException_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var service = new StubUnityTestRunService(_ => throw new ArgumentException("invalid"));
            var handler = new TestRunUnityIpcMethodHandler(service);
            var request = CreateTestRunRequest(
                "req-test-run-invalid-argument",
                CreateValidTestRunPayload());

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenServiceThrowsUnexpectedException_ReturnsInternalError () => UniTask.ToCoroutine(async () =>
        {
            var service = new StubUnityTestRunService(_ => throw new InvalidOperationException("test-run-failed"));
            var handler = new TestRunUnityIpcMethodHandler(service);
            var request = CreateTestRunRequest(
                "req-test-run-internal-error",
                CreateValidTestRunPayload());

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InternalError));
            Assert.That(response.Errors[0].Message, Does.Contain("test-run-failed"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = new TestRunUnityIpcMethodHandler(
                new StubUnityTestRunService(request => Task.FromResult(UnityTestRunServiceResult.Success(new IpcTestRunResponse(0)))));
            var request = CreateTestRunRequest("req-test-run-invalid-payload", 123);

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ShutdownHandler_WhenPayloadIsValid_ReturnsAcceptedResponse () => UniTask.ToCoroutine(async () =>
        {
            var handler = new ShutdownUnityIpcMethodHandler();
            var request = CreateShutdownRequest("req-shutdown-valid", new IpcShutdownRequest("tests"));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcShutdownResponse payload, out _), Is.True);
            Assert.That(payload.Accepted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ShutdownHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = new ShutdownUnityIpcMethodHandler();
            var request = CreateShutdownRequest("req-shutdown-invalid", 123);

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DaemonLogsReadHandler_WhenPayloadIsValid_ReturnsFilteredEventsAndNextCursor () => UniTask.ToCoroutine(async () =>
        {
            var stream = new DaemonLogRingBuffer();
            stream.Write("ipc", "info", "server started");
            stream.Write("transport", "warning", "socket timeout detected", "SocketException");
            var snapshot = stream.Snapshot();
            var firstEventCursor = snapshot.Events[0].Cursor;
            var handler = CreateDaemonLogsReadHandler(stream);
            var request = CreateDaemonLogsReadRequest(
                "req-daemon-logs-valid",
                new IpcDaemonLogsReadRequest(
                    Tail: null,
                    After: firstEventCursor,
                    Since: null,
                    Until: null,
                    Level: "warning",
                    Query: "socket",
                    QueryTarget: "both",
                    Category: "transport"));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcDaemonLogsReadResponse payload, out _), Is.True);
            Assert.That(payload.Events.Length, Is.EqualTo(1));
            Assert.That(payload.Events[0].Category, Is.EqualTo("transport"));
            Assert.That(payload.Events[0].Level, Is.EqualTo("warning"));
            Assert.That(payload.NextCursor, Does.StartWith(snapshot.StreamId + ":"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DaemonLogsReadHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateDaemonLogsReadHandler(new DaemonLogRingBuffer());
            var request = CreateDaemonLogsReadRequest("req-daemon-logs-invalid", 123);

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DaemonLogsReadHandler_WhenQueryTargetIsStack_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var stream = new DaemonLogRingBuffer();
            stream.Write("ipc", "info", "server started");
            var handler = CreateDaemonLogsReadHandler(stream);
            var request = CreateDaemonLogsReadRequest(
                "req-daemon-logs-stack",
                new IpcDaemonLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: "socket",
                    QueryTarget: "stack",
                    Category: null));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DaemonLogsReadHandler_WhenCategoryIsAll_DoesNotFilterByCategory () => UniTask.ToCoroutine(async () =>
        {
            var stream = new DaemonLogRingBuffer();
            stream.Write("ipc", "info", "server started");
            stream.Write("transport", "warning", "socket timeout detected");
            var handler = CreateDaemonLogsReadHandler(stream);
            var request = CreateDaemonLogsReadRequest(
                "req-daemon-logs-category-all",
                new IpcDaemonLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Category: "all"));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcDaemonLogsReadResponse payload, out _), Is.True);
            Assert.That(payload.Events.Length, Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DaemonLogsReadHandler_WhenAfterAndSinceSpecified_AfterHasPriority () => UniTask.ToCoroutine(async () =>
        {
            var stream = new DaemonLogRingBuffer();
            stream.Write("ipc", "info", "before");
            stream.Write("transport", "info", "after");
            var snapshot = stream.Snapshot();
            var secondCursor = snapshot.Events[1].Cursor;
            var since = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
            var handler = CreateDaemonLogsReadHandler(stream);
            var request = CreateDaemonLogsReadRequest(
                "req-daemon-logs-after-priority",
                new IpcDaemonLogsReadRequest(
                    Tail: null,
                    After: secondCursor,
                    Since: since,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Category: null));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcDaemonLogsReadResponse payload, out _), Is.True);
            Assert.That(payload.Events.Length, Is.EqualTo(1));
            Assert.That(payload.Events[0].Message, Is.EqualTo("after"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenPayloadIsValid_ReturnsFilteredEventsAndNextCursor () => UniTask.ToCoroutine(async () =>
        {
            var stream = new UnityLogRingBuffer();
            stream.Write(IpcUnityLogsSourceCodec.Runtime, IpcDaemonLogsLevelCodec.Info, "player joined");
            stream.Write(IpcUnityLogsSourceCodec.Compile, IpcDaemonLogsLevelCodec.Warning, "Assets/Test.cs(1,2): warning CS0168");
            var snapshot = stream.Snapshot();
            var handler = CreateUnityLogsReadHandler(stream);
            var request = CreateUnityLogsReadRequest(
                "req-unity-logs-valid",
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: "info",
                    Query: "player",
                    QueryTarget: "message",
                    Source: "runtime",
                    StackTrace: "all",
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcUnityLogsReadResponse payload, out _), Is.True);
            Assert.That(payload.Events.Length, Is.EqualTo(1));
            Assert.That(payload.Events[0].Source, Is.EqualTo("runtime"));
            Assert.That(payload.Events[0].Message, Is.EqualTo("player joined"));
            Assert.That(payload.NextCursor, Is.EqualTo(snapshot.NextCursor));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateUnityLogsReadHandler(new UnityLogRingBuffer());
            var request = CreateUnityLogsReadRequest("req-unity-logs-invalid", 123);

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenAfterCursorIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateUnityLogsReadHandler(new UnityLogRingBuffer());
            var request = CreateUnityLogsReadRequest(
                "req-unity-logs-invalid-after",
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: "invalid-cursor",
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Source: null,
                    StackTrace: null,
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenSourceIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateUnityLogsReadHandler(new UnityLogRingBuffer());
            var request = CreateUnityLogsReadRequest(
                "req-unity-logs-invalid-source",
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Source: "editor",
                    StackTrace: null,
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenStackTraceModeIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateUnityLogsReadHandler(new UnityLogRingBuffer());
            var request = CreateUnityLogsReadRequest(
                "req-unity-logs-invalid-stack-trace",
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Source: null,
                    StackTrace: "warningsOnly",
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenQueryTargetIsStack_ReturnsStackMatches () => UniTask.ToCoroutine(async () =>
        {
            var stream = new UnityLogRingBuffer();
            stream.Write(IpcUnityLogsSourceCodec.Runtime, IpcDaemonLogsLevelCodec.Error, "runtime error", "SocketException: broken pipe");
            var handler = CreateUnityLogsReadHandler(stream);
            var request = CreateUnityLogsReadRequest(
                "req-unity-logs-stack-query",
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: "SocketException",
                    QueryTarget: "stack",
                    Source: null,
                    StackTrace: "all",
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcUnityLogsReadResponse payload, out _), Is.True);
            Assert.That(payload.Events.Length, Is.EqualTo(1));
            Assert.That(payload.Events[0].StackTrace, Does.Contain("SocketException"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenAfterAndSinceSpecified_AfterHasPriority () => UniTask.ToCoroutine(async () =>
        {
            var stream = new UnityLogRingBuffer();
            stream.Write(IpcUnityLogsSourceCodec.Runtime, IpcDaemonLogsLevelCodec.Info, "before");
            stream.Write(IpcUnityLogsSourceCodec.Compile, IpcDaemonLogsLevelCodec.Warning, "after");
            var snapshot = stream.Snapshot();
            var secondCursor = snapshot.Events[1].Cursor;
            var since = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
            var handler = CreateUnityLogsReadHandler(stream);
            var request = CreateUnityLogsReadRequest(
                "req-unity-logs-after-priority",
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: secondCursor,
                    Since: since,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Source: null,
                    StackTrace: null,
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcUnityLogsReadResponse payload, out _), Is.True);
            Assert.That(payload.Events.Length, Is.EqualTo(1));
            Assert.That(payload.Events[0].Message, Is.EqualTo("after"));
        });

        private static object CreateValidTestRunPayload (bool failFast = false)
        {
            return new IpcTestRunRequest(
                TestPlatform: IpcTestRunPlatformCodec.EditMode,
                BuildTarget: null,
                TestFilter: null,
                TestCategories: Array.Empty<string>(),
                AssemblyNames: Array.Empty<string>(),
                TestSettingsPath: null,
                ResultsXmlPath: "/tmp/results.xml",
                EditorLogPath: "/tmp/editor.log",
                FailFast: failFast);
        }

        private static IpcRequest CreatePingRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.Ping, payload);
        }

        private static IpcRequest CreateExecuteRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.Execute, payload);
        }

        private static IpcRequest CreateTestRunRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.TestRun, payload);
        }

        private static IpcRequest CreateShutdownRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.Shutdown, payload);
        }

        private static IpcRequest CreateDaemonLogsReadRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.DaemonLogsRead, payload);
        }

        private static IpcRequest CreateUnityLogsReadRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.UnityLogsRead, payload);
        }

        private static DaemonLogsReadUnityIpcMethodHandler CreateDaemonLogsReadHandler (IDaemonLogStream stream)
        {
            return new DaemonLogsReadUnityIpcMethodHandler(
                stream,
                new DaemonLogsReadRequestValidator(),
                new DaemonLogsReadQueryEngine(),
                new DaemonLogsReadResponseFactory());
        }

        private static UnityLogsReadUnityIpcMethodHandler CreateUnityLogsReadHandler (IUnityLogStream stream)
        {
            return new UnityLogsReadUnityIpcMethodHandler(
                stream,
                new UnityLogsReadRequestValidator(),
                new UnityLogsReadQueryEngine(),
                new UnityLogsReadResponseFactory());
        }

        private static IpcRequest CreateRequest (
            string requestId,
            string method,
            object payload)
        {
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: "session-token",
                Method: method,
                Payload: IpcPayloadCodec.SerializeToElement(payload));
        }

        private sealed class StubServerVersionProvider : IServerVersionProvider
        {
            private readonly string version;

            public StubServerVersionProvider (string version)
            {
                this.version = version;
            }

            public string GetVersion ()
            {
                return version;
            }
        }

        private sealed class StubExecuteRequestDispatcher : IExecuteRequestDispatcher
        {
            public int CallCount { get; private set; }

            public IpcExecuteRequest LastRequest { get; private set; }

            public ExecuteDispatchContext LastContext { get; private set; }

            public Task<IpcResponse> Dispatch (
                IpcExecuteRequest request,
                ExecuteDispatchContext context,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                LastRequest = request;
                LastContext = context;
                return Task.FromResult(new IpcResponse(
                    ProtocolVersion: context.ProtocolVersion,
                    RequestId: context.RequestId,
                    Status: IpcProtocol.StatusOk,
                    Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>())),
                    Errors: Array.Empty<IpcError>()));
            }
        }

        private sealed class StubUnityTestRunService : IUnityTestRunService
        {
            private readonly Func<IpcTestRunRequest, Task<UnityTestRunServiceResult>> execute;

            public StubUnityTestRunService (Func<IpcTestRunRequest, Task<UnityTestRunServiceResult>> execute)
            {
                this.execute = execute;
            }

            public int CallCount { get; private set; }

            public IpcTestRunRequest LastRequest { get; private set; }

            public Task<UnityTestRunServiceResult> Execute (
                IpcTestRunRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                LastRequest = request;
                return execute(request);
            }
        }

    }
}
