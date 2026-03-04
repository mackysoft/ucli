using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>ping</c> IPC method requests. </summary>
    internal sealed class PingUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly IServerVersionProvider serverVersionProvider;

        /// <summary> Initializes a new instance of the <see cref="PingUnityIpcMethodHandler" /> class. </summary>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        public PingUnityIpcMethodHandler (IServerVersionProvider serverVersionProvider)
        {
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.Ping;

        /// <inheritdoc />
        public ValueTask<IpcResponse> Handle (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodePingRequest(
                    request,
                    out IpcPingRequest _,
                    out var errorResponse))
            {
                return new ValueTask<IpcResponse>(errorResponse!);
            }

            var payload = UnityPingResponseCodec.CreatePayload(
                Application.unityVersion,
                serverVersionProvider.GetVersion(),
                EditorApplication.isCompiling);
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(request, payload));
        }
    }
}
