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
    public sealed class UnityIpcConnectionHandlerTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenMalformedFrameAndErrorResponseWriteFails_DoesNotThrow () => UniTask.ToCoroutine(async () =>
        {
            var requestProcessor = new StubRequestProcessor();
            var handler = new UnityIpcConnectionHandler(requestProcessor);
            using var stream = new ThrowOnReadAndWriteStream();

            await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(requestProcessor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StreamFrameWriter_WhenProgressWriteFails_InvokesWriteFailureHandler () => UniTask.ToCoroutine(async () =>
        {
            var request = new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "req-stream-write-failure",
                SessionToken: "token",
                Method: IpcMethodNames.Shutdown,
                Payload: JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: IpcResponseMode.Stream);
            using var stream = new ThrowOnWriteStream();
            Exception observedFailure = null;
            var streamWriter = new IpcStreamFrameWriter(
                stream,
                request,
                exception => observedFailure = exception);

            IOException exception = null;
            try
            {
                await streamWriter.WriteProgressAsync(
                    "test.progress",
                    new UcliEmptyArgs(),
                    CancellationToken.None);
            }
            catch (IOException writeException)
            {
                exception = writeException;
            }

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.Message, Does.Contain("write failed"));
            Assert.That(observedFailure, Is.SameAs(exception));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenShutdownResponseWritten_ReturnsRequestAndResponse () => UniTask.ToCoroutine(async () =>
        {
            var requestProcessor = new StubRequestProcessor();
            var handler = new UnityIpcConnectionHandler(requestProcessor);
            var request = new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "req-shutdown",
                SessionToken: "token",
                Method: IpcMethodNames.Shutdown,
                Payload: JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: IpcResponseMode.Single);

            using var stream = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            stream.Position = 0;

            var result = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(result.Request, Is.Not.Null);
            Assert.That(result.Request.Method, Is.EqualTo(IpcMethodNames.Shutdown));
            Assert.That(result.Response, Is.Not.Null);
            Assert.That(result.Response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(result.Response.Errors, Is.Empty);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenResponseModeIsStream_WritesProgressAndTerminalFrames () => UniTask.ToCoroutine(async () =>
        {
            var requestProcessor = new StubStreamingRequestProcessor();
            var handler = new UnityIpcConnectionHandler(requestProcessor);
            var request = new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "req-stream",
                SessionToken: "token",
                Method: IpcMethodNames.Shutdown,
                Payload: JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: IpcResponseMode.Stream);

            using var stream = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            var responseStartPosition = stream.Position;
            stream.Position = 0;

            var result = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(result.Request, Is.Not.Null);
            Assert.That(result.Response, Is.Not.Null);
            Assert.That(requestProcessor.CallCount, Is.EqualTo(0));
            Assert.That(requestProcessor.StreamingCallCount, Is.EqualTo(1));

            stream.Position = responseStartPosition;
            var progressFrameResult = await IpcFrameCodec.TryReadModelAsync<IpcStreamFrame>(
                stream,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            var terminalFrameResult = await IpcFrameCodec.TryReadModelAsync<IpcStreamFrame>(
                stream,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);

            Assert.That(progressFrameResult.IsSuccess, Is.True);
            Assert.That(progressFrameResult.Value.Kind, Is.EqualTo(IpcStreamFrameKinds.Progress));
            Assert.That(progressFrameResult.Value.RequestId, Is.EqualTo("req-stream"));
            Assert.That(progressFrameResult.Value.Event, Is.EqualTo("test.progress"));

            Assert.That(terminalFrameResult.IsSuccess, Is.True);
            Assert.That(terminalFrameResult.Value.Kind, Is.EqualTo(IpcStreamFrameKinds.Terminal));
            Assert.That(terminalFrameResult.Value.Response, Is.Not.Null);
            Assert.That(terminalFrameResult.Value.Response.Status, Is.EqualTo(IpcProtocol.StatusOk));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RequestHandler_WhenStreamRequestUsesSinglePath_ReturnsInvalidArgumentWithoutDispatch () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = new StubMethodDispatcher();
            var handler = new UnityIpcRequestHandler(new StubSessionTokenValidator(true), dispatcher);
            var request = CreateShutdownRequest("req-mode-mismatch-single", IpcResponseMode.Stream);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(dispatcher.DispatchCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.StreamingDispatchCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RequestHandler_WhenSingleRequestUsesStreamingPath_ReturnsInvalidArgumentWithoutDispatch () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = new StubMethodDispatcher();
            var handler = new UnityIpcRequestHandler(new StubSessionTokenValidator(true), dispatcher);
            var request = CreateShutdownRequest("req-mode-mismatch-stream", IpcResponseMode.Single);

            var response = await handler.HandleStreamingAsync(request, new NoOpStreamFrameWriter(), CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(dispatcher.DispatchCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.StreamingDispatchCallCount, Is.EqualTo(0));
        });

        private static IpcRequest CreateShutdownRequest (
            string requestId,
            IpcResponseMode responseMode)
        {
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: "token",
                Method: IpcMethodNames.Shutdown,
                Payload: JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: responseMode);
        }

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

            public Task<IpcResponse> ProcessStreamingAsync (
                IpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class StubMethodDispatcher : IUnityIpcMethodDispatcher
        {
            public int DispatchCallCount { get; private set; }

            public int StreamingDispatchCallCount { get; private set; }

            public Task<IpcResponse> DispatchAsync (
                IpcRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DispatchCallCount++;
                return Task.FromResult(CreateResponse(request));
            }

            public Task<IpcResponse> DispatchStreamingAsync (
                IpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                StreamingDispatchCallCount++;
                return Task.FromResult(CreateResponse(request));
            }

            private static IpcResponse CreateResponse (IpcRequest request)
            {
                return new IpcResponse(
                    ProtocolVersion: IpcProtocol.CurrentVersion,
                    RequestId: request.RequestId,
                    Status: IpcProtocol.StatusOk,
                    Payload: JsonSerializer.SerializeToElement(new IpcShutdownResponse(true, "ok")),
                    Errors: Array.Empty<IpcError>());
            }
        }

        private sealed class StubSessionTokenValidator : ISessionTokenValidator
        {
            private readonly bool result;

            public StubSessionTokenValidator (bool result)
            {
                this.result = result;
            }

            public Task<bool> ValidateAsync (
                string sessionToken,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(result);
            }
        }

        private sealed class NoOpStreamFrameWriter : IIpcStreamFrameWriter
        {
            public ValueTask WriteProgressAsync<TPayload> (
                string eventName,
                TPayload payload,
                CancellationToken cancellationToken = default)
                where TPayload : notnull
            {
                cancellationToken.ThrowIfCancellationRequested();
                return default;
            }

            public ValueTask WriteTerminalAsync (
                IpcResponse response,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return default;
            }
        }

        private sealed class StubStreamingRequestProcessor : IUnityIpcRequestProcessor
        {
            public int CallCount { get; private set; }

            public int StreamingCallCount { get; private set; }

            public Task<IpcResponse> ProcessAsync (
                IpcRequest request,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                return Task.FromResult(CreateResponse(request));
            }

            public async Task<IpcResponse> ProcessStreamingAsync (
                IpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                CancellationToken cancellationToken = default)
            {
                StreamingCallCount++;
                await streamWriter.WriteProgressAsync(
                    "test.progress",
                    new UcliEmptyArgs(),
                    cancellationToken);
                return CreateResponse(request);
            }

            private static IpcResponse CreateResponse (IpcRequest request)
            {
                return new IpcResponse(
                    ProtocolVersion: IpcProtocol.CurrentVersion,
                    RequestId: request.RequestId,
                    Status: IpcProtocol.StatusOk,
                    Payload: JsonSerializer.SerializeToElement(new IpcShutdownResponse(true, "ok")),
                    Errors: Array.Empty<IpcError>());
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

        private sealed class ThrowOnWriteStream : Stream
        {
            public override bool CanRead => false;

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
                throw new NotSupportedException();
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
