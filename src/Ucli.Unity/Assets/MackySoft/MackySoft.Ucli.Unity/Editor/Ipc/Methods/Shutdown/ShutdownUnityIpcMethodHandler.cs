using System;
using System.Collections.Generic;
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
        public UnityIpcMethod Method => UnityIpcMethod.Shutdown;

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
        /// <summary> Seals mutation admission for one logical shutdown request and registers the current response exchange. </summary>
        bool TryPrepare (ValidatedUnityIpcRequest request, out string errorMessage);

        /// <summary> Commits the seal after a response exchange for the matching logical request has been written. </summary>
        bool TryCommit (ValidatedUnityIpcRequest request);

        /// <summary> Releases one failed response exchange and the uncommitted seal when no matching exchange remains active. </summary>
        void Abort (ValidatedUnityIpcRequest request);
    }

    /// <summary> Owns the shutdown admission seal until the current host generation is disposed. </summary>
    internal sealed class UnityShutdownAdmissionCoordinator : IUnityShutdownAdmissionCoordinator, IDisposable
    {
        private readonly object syncRoot = new object();

        private readonly IUnityMutationLaneControl mutationLaneControl;

        private readonly List<ValidatedUnityIpcRequest> activeExchanges = new List<ValidatedUnityIpcRequest>();

        private Guid? preparedRequestId;

        private IDisposable admissionSeal;

        private bool isCommitted;

        private bool isDisposed;

        public UnityShutdownAdmissionCoordinator (IUnityMutationLaneControl mutationLaneControl)
        {
            this.mutationLaneControl = mutationLaneControl
                ?? throw new ArgumentNullException(nameof(mutationLaneControl));
        }

        /// <inheritdoc />
        public bool TryPrepare (ValidatedUnityIpcRequest request, out string errorMessage)
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

                if (preparedRequestId.HasValue)
                {
                    if (preparedRequestId.Value == request.RequestId)
                    {
                        if (!isCommitted && !ContainsActiveExchange(request))
                        {
                            activeExchanges.Add(request);
                        }

                        errorMessage = null;
                        return true;
                    }

                    errorMessage = "Another shutdown response exchange already owns mutation admission.";
                    return false;
                }

                if (!mutationLaneControl.TrySealAdmissionForRetirement(out var nextAdmissionSeal))
                {
                    errorMessage = mutationLaneControl.IsQuarantined
                        ? "The quarantined Unity mutation generation could not begin retirement. Retry shutdown."
                        : "A Unity mutation is active or queued. Retry shutdown after it reaches a terminal state.";
                    return false;
                }

                preparedRequestId = request.RequestId;
                activeExchanges.Add(request);
                admissionSeal = nextAdmissionSeal;
                errorMessage = null;
                return true;
            }
        }

        /// <inheritdoc />
        public bool TryCommit (ValidatedUnityIpcRequest request)
        {
            if (request == null)
            {
                return false;
            }

            lock (syncRoot)
            {
                if (isDisposed
                    || preparedRequestId != request.RequestId
                    || (!isCommitted && !ContainsActiveExchange(request)))
                {
                    return false;
                }

                isCommitted = true;
                activeExchanges.Clear();
                return true;
            }
        }

        /// <inheritdoc />
        public void Abort (ValidatedUnityIpcRequest request)
        {
            IDisposable sealToRelease = null;
            lock (syncRoot)
            {
                if (isDisposed
                    || isCommitted
                    || preparedRequestId != request.RequestId
                    || !RemoveActiveExchange(request))
                {
                    return;
                }

                if (activeExchanges.Count != 0)
                {
                    return;
                }

                preparedRequestId = null;
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
                preparedRequestId = null;
                activeExchanges.Clear();
                sealToRelease = admissionSeal;
                admissionSeal = null;
            }

            sealToRelease?.Dispose();
        }

        private bool ContainsActiveExchange (ValidatedUnityIpcRequest request)
        {
            return FindActiveExchangeIndex(request) >= 0;
        }

        private bool RemoveActiveExchange (ValidatedUnityIpcRequest request)
        {
            var index = FindActiveExchangeIndex(request);
            if (index < 0)
            {
                return false;
            }

            activeExchanges.RemoveAt(index);
            return true;
        }

        private int FindActiveExchangeIndex (ValidatedUnityIpcRequest request)
        {
            for (var i = 0; i < activeExchanges.Count; i++)
            {
                if (ReferenceEquals(activeExchanges[i], request))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
