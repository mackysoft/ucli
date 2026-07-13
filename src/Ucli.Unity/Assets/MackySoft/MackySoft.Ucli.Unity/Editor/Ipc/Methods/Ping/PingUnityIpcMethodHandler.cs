using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>ping</c> IPC method requests. </summary>
    internal sealed class PingUnityIpcMethodHandler : IUnityControlPlaneIpcMethodHandler
    {
        private readonly IServerVersionProvider serverVersionProvider;
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly string projectFingerprint;
        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="PingUnityIpcMethodHandler" /> class. </summary>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <param name="projectFingerprint"> The project fingerprint served by this IPC host. </param>
        public PingUnityIpcMethodHandler (
            IServerVersionProvider serverVersionProvider,
            IUnityEditorReadinessGate readinessGate,
            string projectFingerprint,
            IDaemonLogger daemonLogger)
        {
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("projectFingerprint must not be empty.", nameof(projectFingerprint));
            }

            this.projectFingerprint = projectFingerprint;
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        /// <inheritdoc />
        public UnityIpcMethod Method => UnityIpcMethod.Ping;

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

            if (!UnityIpcRequestCodec.TryDecodePingRequest(
                    request,
                    out IpcPingRequest _,
                    out var errorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Health,
                    "Ping payload decode failed.");
                return new ValueTask<IpcResponse>(errorResponse!);
            }

            var payload = UnityLifecycleResponseCodec.CreatePingPayload(
                Application.unityVersion,
                serverVersionProvider.GetVersion(),
                projectFingerprint,
                readinessGate.CaptureSnapshot());
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(request, payload));
        }
    }
}
