using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test");
            await server.Start(endpoint).AsUniTask();
            Assert.That(server.IsRunning, Is.True);

            await server.Stop().AsUniTask();
            Assert.That(server.IsRunning, Is.False);
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
                static () => { },
                new IUnityIpcTransportListener[]
                {
                    new ThrowingTransportListener(IpcTransportKind.NamedPipe, "listener failed"),
                });
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test-failure");
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: listener failed"));

            await AsyncExceptionCapture.CaptureAsync<InvalidOperationException>(async () =>
            {
                await server.Start(endpoint).AsUniTask();
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
                static () => { });
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
                static () => { });
            var request = CreatePingRequest(sessionToken: "invalid-token");

            var response = await server.HandleRequest(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.SessionTokenInvalid));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleRequest_WhenValidTokenAndPing_ReturnsPingResponse () => UniTask.ToCoroutine(async () =>
        {
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                static () => { });
            var request = CreatePingRequest(sessionToken: "valid-token");

            var response = await server.HandleRequest(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            var payload = response.Payload.Deserialize<IpcPingResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Runtime, Is.EqualTo("batchmode"));
            Assert.That(string.IsNullOrWhiteSpace(payload.UnityVersion), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleRequest_WhenValidTokenAndExecute_CallsDispatcher () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = new StubExecuteRequestDispatcher();
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                dispatcher,
                static () => { });
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
            var shutdownSignaled = false;
            var server = CreateServerForRequestHandling(
                new StubSessionTokenValidator(accepted: true),
                new StubExecuteRequestDispatcher(),
                () => shutdownSignaled = true);
            var request = CreateShutdownRequest(sessionToken: "valid-token", requestId: "req-shutdown");

            var response = await server.HandleRequest(request);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            var payload = response.Payload.Deserialize<IpcShutdownResponse>(SerializerOptions);
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Accepted, Is.True);
            Assert.That(shutdownSignaled, Is.True);
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
                new IpcExecuteRequest(IpcExecuteCommandNames.Validate, arguments),
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

        private static UnityIpcServer CreateServerForLifecycle ()
        {
            return CreateServer(
                new PermitAllSessionTokenValidator(),
                new StubExecuteRequestDispatcher(),
                static () => { },
                new IUnityIpcTransportListener[]
                {
                    new NamedPipeUnityIpcTransportListener(),
                    new UnixDomainSocketUnityIpcTransportListener(),
                });
        }

        private static UnityIpcServer CreateServerForRequestHandling (
            ISessionTokenValidator sessionTokenValidator,
            IExecuteRequestDispatcher executeRequestDispatcher,
            Action shutdownSignal)
        {
            return CreateServer(
                sessionTokenValidator,
                executeRequestDispatcher,
                shutdownSignal,
                Array.Empty<IUnityIpcTransportListener>());
        }

        private static UnityIpcServer CreateServer (
            ISessionTokenValidator sessionTokenValidator,
            IExecuteRequestDispatcher executeRequestDispatcher,
            Action shutdownSignal,
            IReadOnlyList<IUnityIpcTransportListener> transportListeners)
        {
            var methodDispatcher = new UnityIpcMethodDispatcher(executeRequestDispatcher, shutdownSignal);
            var requestHandler = new UnityIpcRequestHandler(sessionTokenValidator, methodDispatcher);
            var connectionHandler = new UnityIpcConnectionHandler(requestHandler);
            return new UnityIpcServer(requestHandler, connectionHandler, transportListeners);
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

            public void Run (
                string address,
                IUnityIpcConnectionHandler connectionHandler,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException(message);
            }

            public void Release ()
            {
            }
        }
    }
}
