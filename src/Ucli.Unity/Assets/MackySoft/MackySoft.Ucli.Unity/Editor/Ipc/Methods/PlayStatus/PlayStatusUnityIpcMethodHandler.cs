using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>play.status</c> IPC method requests. </summary>
    internal sealed class PlayStatusUnityIpcMethodHandler : IUnityControlPlaneIpcMethodHandler
    {
        private readonly IServerVersionProvider serverVersionProvider;
        private readonly IUnityEditorAvailabilityObservationSource availabilityObservationSource;
        private readonly IpcProjectIdentity projectIdentity;
        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="PlayStatusUnityIpcMethodHandler" /> class. </summary>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <param name="availabilityObservationSource"> The externally observable Unity Editor availability source. </param>
        /// <param name="projectIdentity"> The project identity served by this IPC host. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        public PlayStatusUnityIpcMethodHandler (
            IServerVersionProvider serverVersionProvider,
            IUnityEditorAvailabilityObservationSource availabilityObservationSource,
            IpcProjectIdentity projectIdentity,
            IDaemonLogger daemonLogger)
        {
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.availabilityObservationSource = availabilityObservationSource
                ?? throw new ArgumentNullException(nameof(availabilityObservationSource));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        /// <inheritdoc />
        public UnityIpcMethod Method => UnityIpcMethod.PlayStatus;

        /// <inheritdoc />
        public ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodePlayStatusRequest(
                    request,
                    out IpcPlayStatusRequest _,
                    out var errorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Health,
                    "Play status payload decode failed.");
                return new ValueTask<IpcResponse>(errorResponse!);
            }

            var payload = new IpcPlayStatusResponse(UnityLifecycleResponseFactory.Create(
                projectIdentity,
                serverVersionProvider.GetVersion(),
                availabilityObservationSource.CaptureAvailabilityObservation()));
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(request, payload));
        }
    }
}
