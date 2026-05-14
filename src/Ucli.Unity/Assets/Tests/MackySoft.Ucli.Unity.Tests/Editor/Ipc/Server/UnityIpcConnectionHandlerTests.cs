using System;
using System.Collections;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcConnectionHandlerTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenMalformedFrameAndErrorResponseWriteFails_DoesNotThrow () => UniTask.ToCoroutine(async () =>
        {
            var requestProcessor = new StubRequestProcessor();
            var handler = new UnityIpcConnectionHandler(
                requestProcessor,
                new StubDaemonShutdownSignal());
            using var stream = new ThrowOnReadAndWriteStream();

            await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(requestProcessor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenShutdownResponseWritten_SignalsShutdownAfterWrite () => UniTask.ToCoroutine(async () =>
        {
            var shutdownSignal = new StubDaemonShutdownSignal();
            var requestProcessor = new StubRequestProcessor();
            var handler = new UnityIpcConnectionHandler(
                requestProcessor,
                shutdownSignal);
            var request = new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "req-shutdown",
                SessionToken: "token",
                Method: IpcMethodNames.Shutdown,
                Payload: JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")));

            using var stream = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            stream.Position = 0;

            await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(shutdownSignal.SignalCount, Is.EqualTo(1));
        });

        private sealed class StubRequestProcessor : IUnityIpcRequestProcessor
        {
            public int CallCount { get; private set; }

            public Task<IpcResponse> ProcessAsync (
                IpcRequest request,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                return Task.FromResult(new IpcResponse(
                    ProtocolVersion: IpcProtocol.CurrentVersion,
                    RequestId: request.RequestId,
                    Status: IpcProtocol.StatusOk,
                    Payload: JsonSerializer.SerializeToElement(new IpcShutdownResponse(true, "ok")),
                    Errors: Array.Empty<IpcError>()));
            }
        }

        private sealed class StubDaemonShutdownSignal : IDaemonShutdownSignal
        {
            public int SignalCount { get; private set; }

            public bool IsSignaled => SignalCount > 0;

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

        private sealed class ThrowOnReadAndWriteStream : Stream
        {
            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => 0;

            public override long Position
            {
                get => 0;
                set => throw new NotSupportedException();
            }

            public override void Flush ()
            {
            }

            public override int Read (
                byte[] buffer,
                int offset,
                int count)
            {
                throw new InvalidDataException("read failed");
            }

            public override Task<int> ReadAsync (
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                return Task.FromException<int>(new InvalidDataException("read failed"));
            }

            public override long Seek (
                long offset,
                SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength (long value)
            {
                throw new NotSupportedException();
            }

            public override void Write (
                byte[] buffer,
                int offset,
                int count)
            {
                throw new IOException("write failed");
            }

            public override Task WriteAsync (
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                return Task.FromException(new IOException("write failed"));
            }
        }
    }
}
