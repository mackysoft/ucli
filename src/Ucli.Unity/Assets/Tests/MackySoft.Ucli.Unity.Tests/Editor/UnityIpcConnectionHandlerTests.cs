using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
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
            var requestHandler = new StubRequestHandler();
            var handler = new UnityIpcConnectionHandler(requestHandler);
            using var stream = new ThrowOnReadAndWriteStream();

            await handler.Handle(stream, CancellationToken.None);

            Assert.That(requestHandler.CallCount, Is.EqualTo(0));
        });

        private sealed class StubRequestHandler : IUnityIpcRequestHandler
        {
            public int CallCount { get; private set; }

            public Task<IpcResponse> Handle (
                IpcRequest request,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                return Task.FromResult(new IpcResponse(
                    ProtocolVersion: IpcProtocol.CurrentVersion,
                    RequestId: "req",
                    Status: IpcProtocol.StatusOk,
                    Payload: default,
                    Errors: Array.Empty<IpcError>()));
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
