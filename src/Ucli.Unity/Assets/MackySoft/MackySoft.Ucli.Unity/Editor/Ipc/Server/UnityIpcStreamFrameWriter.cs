using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Writes length-prefixed IPC stream frames to one connected transport stream. </summary>
    internal sealed class UnityIpcStreamFrameWriter : IUnityIpcStreamFrameWriter
    {
        private readonly Stream stream;
        private readonly string requestId;
        private readonly Action<Exception> writeFailureHandler;
        private readonly SemaphoreSlim writeGate = new SemaphoreSlim(1, 1);

        /// <summary> Initializes a new instance of the <see cref="UnityIpcStreamFrameWriter" /> class. </summary>
        public UnityIpcStreamFrameWriter (
            Stream stream,
            IpcRequest request,
            Action<Exception> writeFailureHandler = null)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            requestId = request.RequestId;
            this.writeFailureHandler = writeFailureHandler;
        }

        /// <inheritdoc />
        public async Task WriteProgressAsync (
            string eventName,
            object payload,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("Progress event name must not be empty.", nameof(eventName));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var frame = new IpcStreamFrame(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                Kind: IpcStreamFrameKinds.Progress,
                Event: eventName,
                Payload: IpcPayloadCodec.SerializeToElement(payload),
                Response: null);
            await WriteFrameAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task WriteTerminalAsync (
            IpcResponse response,
            CancellationToken cancellationToken = default)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            var frame = new IpcStreamFrame(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                Kind: IpcStreamFrameKinds.Terminal,
                Event: null,
                Payload: IpcPayloadCodec.SerializeToElement(new UcliEmptyArgs()),
                Response: response);
            await WriteFrameAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        private async Task WriteFrameAsync (
            IpcStreamFrame frame,
            CancellationToken cancellationToken)
        {
            await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                try
                {
                    await IpcFrameCodec.WriteModelAsync(
                        stream,
                        frame,
                        IpcJsonSerializerOptions.Default,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
                {
                    writeFailureHandler?.Invoke(exception);
                    throw;
                }
            }
            finally
            {
                writeGate.Release();
            }
        }
    }
}
