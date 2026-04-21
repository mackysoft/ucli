using System;
using System.Collections;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
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

            using var stream = CreateStream(request);
            var handledResult = await handler.Handle(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(IpcMethodNames.Ping));
            Assert.That(completionSignal.Wait(CancellationToken.None).IsCompleted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenSuccessfulNonPingRequestHandled_SignalsCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(IpcMethodNames.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal);

            using var stream = CreateStream(request);
            var handledResult = await handler.Handle(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(IpcMethodNames.OpsRead));
            Assert.That(completionSignal.Wait(CancellationToken.None).IsCompleted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenNonPingRequestReturnsError_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(IpcMethodNames.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var errorResponse = new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: request.RequestId,
                Status: IpcProtocol.StatusError,
                Payload: JsonSerializer.SerializeToElement(new { }),
                Errors: new[]
                {
                    new IpcError(IpcErrorCodes.InvalidArgument, "invalid", null),
                });
            var handler = CreateHandler(request, errorResponse, completionSignal);

            using var stream = CreateStream(request);
            var handledResult = await handler.Handle(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Request.Method, Is.EqualTo(IpcMethodNames.OpsRead));
            Assert.That(completionSignal.Wait(CancellationToken.None).IsCompleted, Is.False);
        });

        private static UnityOneshotConnectionHandler CreateHandler (
            IpcRequest expectedRequest,
            IpcResponse response,
            OneshotRequestCompletionSignal completionSignal)
        {
            return new UnityOneshotConnectionHandler(
                new UnityIpcConnectionHandler(
                    new StubRequestProcessor(expectedRequest, response),
                    new StubDaemonShutdownSignal()),
                completionSignal);
        }

        private static MemoryStream CreateStream (IpcRequest request)
        {
            var stream = new MemoryStream();
            IpcFrameCodec.WriteModelAsync(
                    stream,
                    request,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: CancellationToken.None)
                .GetAwaiter()
                .GetResult();
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
                Payload: payload);
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

            public Task<IpcResponse> Process (
                IpcRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.That(request.Method, Is.EqualTo(expectedRequest.Method));
                Assert.That(request.RequestId, Is.EqualTo(expectedRequest.RequestId));
                return Task.FromResult(response);
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

        private sealed class StubDaemonShutdownSignal : IDaemonShutdownSignal
        {
            public void Signal ()
            {
            }

            public Task Wait (CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }
    }
}