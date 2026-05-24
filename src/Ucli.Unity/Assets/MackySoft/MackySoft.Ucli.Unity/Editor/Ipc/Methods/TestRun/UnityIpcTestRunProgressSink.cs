using System;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Serializes Unity test-run progress events onto one IPC stream frame writer. </summary>
    internal sealed class UnityIpcTestRunProgressSink : IUnityTestRunProgressSink
    {
        private readonly IUnityIpcStreamFrameWriter streamWriter;
        private readonly CancellationToken requestCancellationToken;

        private readonly object syncRoot = new object();

        private Task tail = Task.CompletedTask;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcTestRunProgressSink" /> class. </summary>
        public UnityIpcTestRunProgressSink (
            IUnityIpcStreamFrameWriter streamWriter,
            CancellationToken requestCancellationToken)
        {
            this.streamWriter = streamWriter ?? throw new ArgumentNullException(nameof(streamWriter));
            this.requestCancellationToken = requestCancellationToken;
        }

        /// <inheritdoc />
        public void Publish (
            string eventName,
            object payload)
        {
            requestCancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("Progress event name must not be empty.", nameof(eventName));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            lock (syncRoot)
            {
                tail = WriteAfterPreviousAsync(tail, streamWriter, eventName, payload, requestCancellationToken);
            }
        }

        /// <inheritdoc />
        public async Task FlushAsync (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task pending;
            lock (syncRoot)
            {
                pending = tail;
            }

            await pending;
            cancellationToken.ThrowIfCancellationRequested();
        }

        private static async Task WriteAfterPreviousAsync (
            Task previous,
            IUnityIpcStreamFrameWriter streamWriter,
            string eventName,
            object payload,
            CancellationToken cancellationToken)
        {
            await previous;
            cancellationToken.ThrowIfCancellationRequested();
            await streamWriter.WriteProgressAsync(eventName, payload, cancellationToken);
        }
    }
}
