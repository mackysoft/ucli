using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>shutdown</c> IPC method requests. </summary>
    internal sealed class ShutdownUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        /// <inheritdoc />
        public string Method => IpcMethodNames.Shutdown;

        /// <inheritdoc />
        public ValueTask<IpcResponse> Handle (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(request);

            if (!UnityIpcRequestCodec.TryDecodeShutdownRequest(
                    request,
                    out IpcShutdownRequest _,
                    out var errorResponse))
            {
                return ValueTask.FromResult(errorResponse!);
            }

            var payload = new IpcShutdownResponse(
                Accepted: true,
                Message: "Shutdown request accepted.");
            return ValueTask.FromResult(UnityIpcResponseFactory.CreateSuccessResponse(request, payload));
        }
    }
}
