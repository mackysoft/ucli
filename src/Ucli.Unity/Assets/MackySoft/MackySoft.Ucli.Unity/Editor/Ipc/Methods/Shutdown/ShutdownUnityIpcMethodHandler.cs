using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>shutdown</c> IPC method requests. </summary>
    internal sealed class ShutdownUnityIpcMethodHandler : IUnityControlPlaneIpcMethodHandler
    {
        private readonly IDaemonLogger daemonLogger;

        private readonly IUnityShutdownAdmissionCoordinator shutdownAdmissionCoordinator;

        /// <summary> Initializes a new instance of the <see cref="ShutdownUnityIpcMethodHandler" /> class. </summary>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        /// <param name="shutdownAdmissionCoordinator"> The shutdown exchange admission coordinator. </param>
        /// <exception cref="ArgumentNullException"> Thrown when a dependency is <see langword="null" />. </exception>
        public ShutdownUnityIpcMethodHandler (
            IDaemonLogger daemonLogger,
            IUnityShutdownAdmissionCoordinator shutdownAdmissionCoordinator)
        {
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
            this.shutdownAdmissionCoordinator = shutdownAdmissionCoordinator ?? throw new ArgumentNullException(nameof(shutdownAdmissionCoordinator));
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

            if (!shutdownAdmissionCoordinator.TryPrepare(request, out var admissionErrorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"Shutdown request rejected. {admissionErrorMessage}");
                return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    EditorLifecycleErrorCodes.EditorBusy,
                    admissionErrorMessage,
                    null));
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

    /// <summary> Coordinates mutation admission with one shutdown response exchange. </summary>
    internal interface IUnityShutdownAdmissionCoordinator
    {
        /// <summary> Seals mutation admission for one decoded shutdown request while the mutation lane is idle. </summary>
        bool TryPrepare (IpcRequest request, out string errorMessage);

        /// <summary> Commits the seal after the matching shutdown response has been written. </summary>
        bool TryCommit (IpcRequest request);

        /// <summary> Releases an uncommitted seal owned by the matching failed exchange. </summary>
        void Abort (IpcRequest request);
    }

    /// <summary> Owns the shutdown admission seal until the current host generation is disposed. </summary>
    internal sealed class UnityShutdownAdmissionCoordinator : IUnityShutdownAdmissionCoordinator, IDisposable
    {
        private readonly object syncRoot = new object();

        private readonly IUnityMutationLaneControl mutationLaneControl;

        private IpcRequest preparedRequest;

        private IDisposable admissionSeal;

        private bool isCommitted;

        private bool isDisposed;

        public UnityShutdownAdmissionCoordinator (IUnityMutationLaneControl mutationLaneControl)
        {
            this.mutationLaneControl = mutationLaneControl
                ?? throw new ArgumentNullException(nameof(mutationLaneControl));
        }

        /// <inheritdoc />
        public bool TryPrepare (IpcRequest request, out string errorMessage)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            lock (syncRoot)
            {
                if (isDisposed)
                {
                    errorMessage = "Shutdown admission is no longer available for this daemon generation.";
                    return false;
                }

                if (preparedRequest != null)
                {
                    if (ReferenceEquals(preparedRequest, request))
                    {
                        errorMessage = null;
                        return true;
                    }

                    errorMessage = "Another shutdown response exchange already owns mutation admission.";
                    return false;
                }

                if (!mutationLaneControl.TrySealAdmissionWhenIdle(out var nextAdmissionSeal))
                {
                    errorMessage = mutationLaneControl.IsPoisoned
                        ? "Unity mutation safety is indeterminate. Restart the Unity Editor to complete shutdown."
                        : "A Unity mutation is active or queued. Retry shutdown after it reaches a terminal state.";
                    return false;
                }

                preparedRequest = request;
                admissionSeal = nextAdmissionSeal;
                errorMessage = null;
                return true;
            }
        }

        /// <inheritdoc />
        public bool TryCommit (IpcRequest request)
        {
            if (request == null)
            {
                return false;
            }

            lock (syncRoot)
            {
                if (isDisposed || !ReferenceEquals(preparedRequest, request))
                {
                    return false;
                }

                isCommitted = true;
                return true;
            }
        }

        /// <inheritdoc />
        public void Abort (IpcRequest request)
        {
            IDisposable sealToRelease = null;
            lock (syncRoot)
            {
                if (isDisposed
                    || isCommitted
                    || !ReferenceEquals(preparedRequest, request))
                {
                    return;
                }

                preparedRequest = null;
                sealToRelease = admissionSeal;
                admissionSeal = null;
            }

            sealToRelease?.Dispose();
        }

        /// <inheritdoc />
        public void Dispose ()
        {
            IDisposable sealToRelease = null;
            lock (syncRoot)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                preparedRequest = null;
                sealToRelease = admissionSeal;
                admissionSeal = null;
            }

            sealToRelease?.Dispose();
        }
    }
}
