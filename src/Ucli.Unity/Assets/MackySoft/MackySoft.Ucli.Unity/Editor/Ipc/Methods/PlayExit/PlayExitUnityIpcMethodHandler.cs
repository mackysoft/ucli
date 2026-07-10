using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>play.exit</c> IPC method requests. </summary>
    internal sealed class PlayExitUnityIpcMethodHandler : IRecoverableUnityIpcMethodHandler
    {
        private readonly PlayExitTransitionRunner transitionRunner;
        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="PlayExitUnityIpcMethodHandler" /> class. </summary>
        /// <param name="transitionRunner"> The transition runner dependency. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        public PlayExitUnityIpcMethodHandler (
            PlayExitTransitionRunner transitionRunner,
            IDaemonLogger daemonLogger)
        {
            this.transitionRunner = transitionRunner ?? throw new ArgumentNullException(nameof(transitionRunner));
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.PlayExit;

        /// <inheritdoc />
        public bool TryCreateRecoverableRequestPayloadHash (
            IpcRequest request,
            out string requestPayloadHash,
            out IpcResponse errorResponse)
        {
            if (!TryReadPlayExitRequest(
                    request,
                    logDecodeFailure: false,
                    out IpcPlayExitRequest exitRequest,
                    out errorResponse))
            {
                requestPayloadHash = null;
                return false;
            }

            // NOTE: timeout is the remaining budget of one replay attempt, not the play-transition identity.
            var stablePayload = IpcPayloadCodec.SerializeToElement(exitRequest with
            {
                TimeoutMilliseconds = null,
            });
            requestPayloadHash = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(stablePayload.GetRawText()));
            return true;
        }

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleAsync (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            return await HandleCoreAsync(request, null, cancellationToken);
        }

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleRecoverableAsync (
            IpcRequest request,
            RecoverableIpcOperationContext context,
            CancellationToken cancellationToken)
        {
            return await HandleCoreAsync(request, context, cancellationToken);
        }

        private async ValueTask<IpcResponse> HandleCoreAsync (
            IpcRequest request,
            RecoverableIpcOperationContext recoverableContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!TryReadPlayExitRequest(
                    request,
                    logDecodeFailure: true,
                    out IpcPlayExitRequest exitRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var result = await transitionRunner.ExitAsync(
                exitRequest.TimeoutMilliseconds!.Value,
                recoverableContext,
                cancellationToken);
            if (result.IsSuccess)
            {
                return UnityIpcResponseFactory.CreateSuccessResponse(request, result.Response);
            }

            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                result.Error.Code,
                result.Error.Message,
                result.Error.OpId,
                result.Response);
        }

        private bool TryReadPlayExitRequest (
            IpcRequest request,
            bool logDecodeFailure,
            out IpcPlayExitRequest exitRequest,
            out IpcResponse errorResponse)
        {
            if (!UnityIpcRequestCodec.TryDecodePlayExitRequest(
                    request,
                    out exitRequest,
                    out errorResponse))
            {
                if (logDecodeFailure)
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Health,
                        "Play exit payload decode failed.");
                }

                return false;
            }

            if (!TryValidateTimeoutMilliseconds(exitRequest!.TimeoutMilliseconds, out var timeoutErrorMessage))
            {
                errorResponse = UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    timeoutErrorMessage,
                    null);
                return false;
            }

            errorResponse = null;
            return true;
        }

        private static bool TryValidateTimeoutMilliseconds (
            int? timeoutMilliseconds,
            out string errorMessage)
        {
            if (!timeoutMilliseconds.HasValue)
            {
                errorMessage = "PlayExit timeoutMilliseconds is required.";
                return false;
            }

            if (timeoutMilliseconds.Value <= 0)
            {
                errorMessage = "PlayExit timeoutMilliseconds must be greater than zero.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
