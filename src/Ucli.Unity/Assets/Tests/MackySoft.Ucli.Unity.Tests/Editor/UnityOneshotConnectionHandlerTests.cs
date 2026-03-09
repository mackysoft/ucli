using System;
using System.Collections;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
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
            var handler = CreateHandler(request, completionSignal);

            using var stream = CreateStream(request);
            var handledRequest = await handler.Handle(stream, CancellationToken.None);

            Assert.That(handledRequest, Is.Not.Null);
            Assert.That(handledRequest.Method, Is.EqualTo(IpcMethodNames.Ping));
            Assert.That(await WaitForSignal(completionSignal, TimeSpan.FromMilliseconds(50)), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenNonPingRequestHandled_SignalsCompletion () => UniTask.ToCoroutine(async () =>
        {
            var completionSignal = new OneshotRequestCompletionSignal();
            var request = CreateRequest(IpcMethodNames.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(request, completionSignal);

            using var stream = CreateStream(request);
            var handledRequest = await handler.Handle(stream, CancellationToken.None);

            Assert.That(handledRequest, Is.Not.Null);
            Assert.That(handledRequest.Method, Is.EqualTo(IpcMethodNames.OpsRead));
            Assert.That(await WaitForSignal(completionSignal, TimeSpan.FromMilliseconds(50)), Is.True);
        });

        private static async Task<bool> WaitForSignal (
            OneshotRequestCompletionSignal completionSignal,
            System.TimeSpan timeout)
        {
            var completionTask = completionSignal.Wait(CancellationToken.None);
            var completedTask = await Task.WhenAny(completionTask, Task.Delay(timeout));
            return ReferenceEquals(completedTask, completionTask);
        }

        private static UnityOneshotConnectionHandler CreateHandler (
            IpcRequest expectedRequest,
            OneshotRequestCompletionSignal completionSignal)
        {
            return new UnityOneshotConnectionHandler(
                new UnityIpcConnectionHandler(
                    new StubRequestProcessor(expectedRequest),
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

            public StubRequestProcessor (IpcRequest expectedRequest)
            {
                this.expectedRequest = expectedRequest;
            }

            public Task<IpcResponse> Process (
                IpcRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.That(request.Method, Is.EqualTo(expectedRequest.Method));
                Assert.That(request.RequestId, Is.EqualTo(expectedRequest.RequestId));
                return Task.FromResult(new IpcResponse(
                    ProtocolVersion: IpcProtocol.CurrentVersion,
                    RequestId: request.RequestId,
                    Status: IpcProtocol.StatusOk,
                    Payload: JsonSerializer.SerializeToElement(new { ok = true }),
                    Errors: System.Array.Empty<IpcError>()));
            }
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
