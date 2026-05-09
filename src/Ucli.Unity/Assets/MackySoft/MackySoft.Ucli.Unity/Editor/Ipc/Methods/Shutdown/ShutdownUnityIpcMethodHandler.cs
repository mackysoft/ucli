using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>shutdown</c> IPC method requests. </summary>
    internal sealed class ShutdownUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="ShutdownUnityIpcMethodHandler" /> class. </summary>
        /// <param name="daemonLogger"> The daemon daemon-logger dependency. </param>
        public ShutdownUnityIpcMethodHandler (IDaemonLogger daemonLogger = null)
        {
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.Shutdown;

        /// <inheritdoc />
        public ValueTask<IpcResponse> HandleAsync (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeShutdownRequest(
                    request,
                    out IpcShutdownRequest _,
                    out var errorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    "Shutdown payload decode failed.");
                return new ValueTask<IpcResponse>(errorResponse!);
            }

            daemonLogger.Info(
                DaemonLogCategories.Lifecycle,
                "Shutdown request accepted.");
            var payload = new IpcShutdownResponse(
                Accepted: true,
                Message: "Shutdown request accepted.");
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(request, payload));
        }
    }
}