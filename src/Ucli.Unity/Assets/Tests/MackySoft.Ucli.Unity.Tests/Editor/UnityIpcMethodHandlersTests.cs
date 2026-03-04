using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcMethodHandlersTests
    {
        [Test]
        [Category("Size.Small")]
        public async Task PingHandler_WhenPayloadIsValid_ReturnsOkResponse ()
        {
            var handler = new PingUnityIpcMethodHandler(new StubServerVersionProvider("1.2.3"));
            var request = CreatePingRequest("req-ping-valid", new IpcPingRequest("client"));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPingResponse payload, out _), Is.True);
            Assert.That(payload.ServerVersion, Is.EqualTo("1.2.3"));
        }

        [Test]
        [Category("Size.Small")]
        public async Task PingHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument ()
        {
            var handler = new PingUnityIpcMethodHandler(new StubServerVersionProvider("1.2.3"));
            var request = CreatePingRequest("req-ping-invalid", 123);

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ExecuteHandler_WhenPayloadIsValid_CallsDispatcher ()
        {
            var dispatcher = new StubExecuteRequestDispatcher();
            var handler = new ExecuteUnityIpcMethodHandler(dispatcher);
            var request = CreateExecuteRequest(
                "req-execute-valid",
                new IpcExecuteRequest(
                    IpcExecuteCommandNames.Validate,
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
            Assert.That(dispatcher.LastRequest.Command, Is.EqualTo(IpcExecuteCommandNames.Validate));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ExecuteHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument ()
        {
            var handler = new ExecuteUnityIpcMethodHandler(new StubExecuteRequestDispatcher());
            var request = CreateExecuteRequest("req-execute-invalid", 123);

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        }

        [Test]
        [Category("Size.Small")]
        public async Task TestRunHandler_WhenServiceSucceeds_ReturnsOkResponse ()
        {
            var service = new StubUnityTestRunService(request => Task.FromResult(new IpcTestRunResponse(2)));
            var handler = new TestRunUnityIpcMethodHandler(service);
            var request = CreateTestRunRequest(
                "req-test-run-success",
                CreateValidTestRunPayload());

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(service.CallCount, Is.EqualTo(1));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcTestRunResponse payload, out _), Is.True);
            Assert.That(payload.ExitCode, Is.EqualTo(2));
        }

        [Test]
        [Category("Size.Small")]
        public async Task TestRunHandler_WhenServiceThrowsArgumentException_ReturnsInvalidArgument ()
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
        }

        [Test]
        [Category("Size.Small")]
        public async Task TestRunHandler_WhenServiceThrowsUnexpectedException_ReturnsInternalError ()
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
        }

        [Test]
        [Category("Size.Small")]
        public async Task TestRunHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument ()
        {
            var handler = new TestRunUnityIpcMethodHandler(
                new StubUnityTestRunService(request => Task.FromResult(new IpcTestRunResponse(0))));
            var request = CreateTestRunRequest("req-test-run-invalid-payload", 123);

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ShutdownHandler_WhenPayloadIsValid_ReturnsAcceptedResponse ()
        {
            var handler = new ShutdownUnityIpcMethodHandler();
            var request = CreateShutdownRequest("req-shutdown-valid", new IpcShutdownRequest("tests"));

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcShutdownResponse payload, out _), Is.True);
            Assert.That(payload.Accepted, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public async Task ShutdownHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument ()
        {
            var handler = new ShutdownUnityIpcMethodHandler();
            var request = CreateShutdownRequest("req-shutdown-invalid", 123);

            var response = await handler.Handle(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
        }

        private static object CreateValidTestRunPayload ()
        {
            return new IpcTestRunRequest(
                TestPlatform: IpcTestRunPlatformCodec.EditMode,
                BuildTarget: null,
                TestFilter: null,
                TestCategories: Array.Empty<string>(),
                AssemblyNames: Array.Empty<string>(),
                TestSettingsPath: null,
                ResultsXmlPath: "/tmp/results.xml",
                EditorLogPath: "/tmp/editor.log");
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
            private readonly Func<IpcTestRunRequest, Task<IpcTestRunResponse>> execute;

            public StubUnityTestRunService (Func<IpcTestRunRequest, Task<IpcTestRunResponse>> execute)
            {
                this.execute = execute;
            }

            public int CallCount { get; private set; }

            public Task<IpcTestRunResponse> Execute (
                IpcTestRunRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return execute(request);
            }
        }
    }
}
