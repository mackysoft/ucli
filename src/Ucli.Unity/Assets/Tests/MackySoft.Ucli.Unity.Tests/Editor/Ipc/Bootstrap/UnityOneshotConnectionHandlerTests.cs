using System;
using System.Collections;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityOneshotConnectionHandlerTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenPingRequestHandled_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(UnityIpcMethod.Ping, JsonSerializer.SerializeToElement(new IpcPingRequest("tests")));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(ContractLiteralCodec.ToValue(UnityIpcMethod.Ping)));
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenReadyPingRequestHandled_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(UnityIpcMethod.Ping, JsonSerializer.SerializeToElement(new IpcPingRequest(IpcPingClientVersions.Ready)));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(ContractLiteralCodec.ToValue(UnityIpcMethod.Ping)));
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenSuccessfulNonPingRequestHandled_SignalsCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(UnityIpcMethod.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(ContractLiteralCodec.ToValue(UnityIpcMethod.OpsRead)));
            Assert.That(completionSignal.IsCompleted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenShutdownRequestHandled_SignalsCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(UnityIpcMethod.Shutdown, JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown)));
            Assert.That(completionSignal.IsCompleted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenNonPingRequestReturnsError_SignalsCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(UnityIpcMethod.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var errorResponse = CreateErrorResponse(request.RequestId, UcliCoreErrorCodes.InvalidArgument);
            var handler = CreateHandler(request, errorResponse, completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(ContractLiteralCodec.ToValue(UnityIpcMethod.OpsRead)));
            Assert.That(completionSignal.IsCompleted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenSessionTokenFailureHandled_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(UnityIpcMethod.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(request, CreateErrorResponse(request.RequestId, IpcSessionErrorCodes.SessionTokenInvalid), completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(ContractLiteralCodec.ToValue(UnityIpcMethod.OpsRead)));
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenProtocolMismatchHandled_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(UnityIpcMethod.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(request, CreateErrorResponse(request.RequestId, IpcProtocolErrorCodes.ProtocolVersionMismatch), completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(ContractLiteralCodec.ToValue(UnityIpcMethod.OpsRead)));
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        private static UnityOneshotConnectionHandler CreateHandler (
            IpcRequest expectedRequest,
            IpcResponse response,
            OneshotRequestCompletionSignal completionSignal)
        {
            return new UnityOneshotConnectionHandler(
                new UnityIpcConnectionHandler(
                    new StubRequestHandler(expectedRequest, response),
                    new PreparedShutdownAdmissionCoordinator(expectedRequest),
                    UnityIpcConnectionHandler.DefaultInitialFrameReadTimeout,
                    UnityIpcConnectionHandler.DefaultResponseFrameWriteTimeout),
                completionSignal);
        }

        private sealed class PreparedShutdownAdmissionCoordinator : IUnityShutdownAdmissionCoordinator
        {
            private readonly IpcRequest preparedRequest;

            public PreparedShutdownAdmissionCoordinator (IpcRequest preparedRequest)
            {
                this.preparedRequest = preparedRequest;
            }

            public bool TryPrepare (IpcRequest request, out string errorMessage)
            {
                errorMessage = null;
                return ReferenceEquals(preparedRequest, request);
            }

            public bool TryCommit (IpcRequest request)
            {
                return ReferenceEquals(preparedRequest, request);
            }

            public void Abort (IpcRequest request)
            {
            }
        }

        private static async Task<MemoryStream> CreateStreamAsync (IpcRequest request)
        {
            var stream = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            stream.Position = 0;
            return stream;
        }

        private static IpcRequest CreateRequest (
            UnityIpcMethod method,
            JsonElement payload)
        {
            return new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "oneshot",
                method: ContractLiteralCodec.ToValue(method),
                payload: payload,
                responseMode: "single");
        }

        private sealed class StubRequestHandler : IUnityIpcRequestHandler
        {
            private readonly IpcRequest expectedRequest;

            private readonly IpcResponse response;

            public StubRequestHandler (
                IpcRequest expectedRequest,
                IpcResponse response)
            {
                this.expectedRequest = expectedRequest;
                this.response = response;
            }

            public Task<IpcResponse> HandleAsync (
                IpcRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.That(request.Method, Is.EqualTo(expectedRequest.Method));
                Assert.That(request.RequestId, Is.EqualTo(expectedRequest.RequestId));
                return Task.FromResult(response);
            }

            public Task<IpcResponse> HandleStreamingAsync (
                IpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private static IpcResponse CreateSuccessResponse (Guid requestId)
        {
            return new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                status: IpcProtocol.StatusOk,
                payload: JsonSerializer.SerializeToElement(new { ok = true }),
                errors: System.Array.Empty<IpcError>());
        }

        private static IpcResponse CreateErrorResponse (
            Guid requestId,
            UcliCode errorCode)
        {
            return new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                status: IpcProtocol.StatusError,
                payload: JsonSerializer.SerializeToElement(new { }),
                errors: new[]
                {
                    new IpcError(errorCode, "error", null),
                });
        }

    }
}
