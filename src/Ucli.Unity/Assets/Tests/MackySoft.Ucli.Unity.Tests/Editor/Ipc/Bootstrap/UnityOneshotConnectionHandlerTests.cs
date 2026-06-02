using System;
using System.Collections;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
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
            var request = CreateRequest(IpcMethodNames.Ping, JsonSerializer.SerializeToElement(new IpcPingRequest("tests")));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(IpcMethodNames.Ping));
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenReadyPingRequestHandled_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(IpcMethodNames.Ping, JsonSerializer.SerializeToElement(new IpcPingRequest(IpcPingClientVersions.Ready)));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(IpcMethodNames.Ping));
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenSuccessfulNonPingRequestHandled_SignalsCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(IpcMethodNames.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(IpcMethodNames.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenShutdownRequestHandled_SignalsCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(IpcMethodNames.Shutdown, JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(IpcMethodNames.Shutdown));
            Assert.That(completionSignal.IsCompleted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenNonPingRequestReturnsError_SignalsCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(IpcMethodNames.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var errorResponse = CreateErrorResponse(request.RequestId, UcliCoreErrorCodes.InvalidArgument);
            var handler = CreateHandler(request, errorResponse, completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(IpcMethodNames.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenSessionTokenFailureHandled_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(IpcMethodNames.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(request, CreateErrorResponse(request.RequestId, IpcSessionErrorCodes.SessionTokenInvalid), completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(IpcMethodNames.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenProtocolMismatchHandled_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(IpcMethodNames.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(request, CreateErrorResponse(request.RequestId, IpcProtocolErrorCodes.ProtocolVersionMismatch), completionSignal);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(IpcMethodNames.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        private static UnityOneshotConnectionHandler CreateHandler (
            IpcRequest expectedRequest,
            IpcResponse response,
            OneshotRequestCompletionSignal completionSignal)
        {
            return new UnityOneshotConnectionHandler(
                new UnityIpcConnectionHandler(new StubRequestProcessor(expectedRequest, response)),
                completionSignal);
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
            string method,
            JsonElement payload)
        {
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: $"req-{method}",
                SessionToken: "oneshot",
                Method: method,
                Payload: payload,
                responseMode: IpcResponseMode.Single);
        }

        private sealed class StubRequestProcessor : IUnityIpcRequestProcessor
        {
            private readonly IpcRequest expectedRequest;

            private readonly IpcResponse response;

            public StubRequestProcessor (
                IpcRequest expectedRequest,
                IpcResponse response)
            {
                this.expectedRequest = expectedRequest;
                this.response = response;
            }

            public Task<IpcResponse> ProcessAsync (
                IpcRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.That(request.Method, Is.EqualTo(expectedRequest.Method));
                Assert.That(request.RequestId, Is.EqualTo(expectedRequest.RequestId));
                return Task.FromResult(response);
            }

            public Task<IpcResponse> ProcessStreamingAsync (
                IpcRequest request,
                IUnityIpcStreamFrameWriter streamWriter,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private static IpcResponse CreateSuccessResponse (string requestId)
        {
            return new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                Status: IpcProtocol.StatusOk,
                Payload: JsonSerializer.SerializeToElement(new { ok = true }),
                Errors: System.Array.Empty<IpcError>());
        }

        private static IpcResponse CreateErrorResponse (
            string requestId,
            UcliCode errorCode)
        {
            return new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                Status: IpcProtocol.StatusError,
                Payload: JsonSerializer.SerializeToElement(new { }),
                Errors: new[]
                {
                    new IpcError(errorCode, "error", null),
                });
        }

    }
}
