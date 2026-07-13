using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>ping</c> IPC method requests. </summary>
    internal sealed class PingUnityIpcMethodHandler : IUnityControlPlaneIpcMethodHandler
    {
        private readonly IServerVersionProvider serverVersionProvider;
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly IpcProjectIdentity projectIdentity;
        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="PingUnityIpcMethodHandler" /> class. </summary>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <param name="projectIdentity"> The project identity served by this IPC host. </param>
        public PingUnityIpcMethodHandler (
            IServerVersionProvider serverVersionProvider,
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            IDaemonLogger daemonLogger)
        {
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
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

            var payload = UnityLifecycleResponseFactory.Create(
                projectIdentity,
                serverVersionProvider.GetVersion(),
                readinessGate.CaptureObservation());
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(request, payload));
        }
    }
}
