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
    /// <summary> Handles <c>play.enter</c> IPC method requests. </summary>
    internal sealed class PlayEnterUnityIpcMethodHandler : IRecoverableUnityIpcMethodHandler
    {
        private readonly PlayEnterTransitionRunner transitionRunner;
        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="PlayEnterUnityIpcMethodHandler" /> class. </summary>
        /// <param name="transitionRunner"> The transition runner dependency. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        public PlayEnterUnityIpcMethodHandler (
            PlayEnterTransitionRunner transitionRunner,
            IDaemonLogger daemonLogger)
        {
            this.transitionRunner = transitionRunner ?? throw new ArgumentNullException(nameof(transitionRunner));
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        /// <inheritdoc />
        public UnityIpcMethod Method => UnityIpcMethod.PlayEnter;

        /// <inheritdoc />
        public bool TryCreateRecoverableRequestPayloadHash (
            ValidatedUnityIpcRequest request,
            out Sha256Digest requestPayloadHash,
            out IpcResponse errorResponse)
        {
            if (!TryReadPlayEnterRequest(
                    request,
                    logDecodeFailure: false,
                    out IpcPlayEnterRequest enterRequest,
                    out errorResponse))
            {
                requestPayloadHash = null;
                return false;
            }

            var stablePayload = IpcPayloadCodec.SerializeToElement(enterRequest);
            requestPayloadHash = Sha256Digest.Compute(Encoding.UTF8.GetBytes(stablePayload.GetRawText()));
            return true;
        }

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            return await HandleCoreAsync(request, null, cancellation);
        }

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleRecoverableAsync (
            ValidatedUnityIpcRequest request,
            RecoverableIpcOperationContext context,
            IpcRequestCancellation cancellation)
        {
            return await HandleCoreAsync(request, context, cancellation);
        }

        private async ValueTask<IpcResponse> HandleCoreAsync (
            ValidatedUnityIpcRequest request,
            RecoverableIpcOperationContext recoverableContext,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!TryReadPlayEnterRequest(
                    request,
                    logDecodeFailure: true,
                    out IpcPlayEnterRequest enterRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var result = await transitionRunner.EnterAsync(
                recoverableContext,
                cancellation);
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

        private bool TryReadPlayEnterRequest (
            ValidatedUnityIpcRequest request,
            bool logDecodeFailure,
            out IpcPlayEnterRequest enterRequest,
            out IpcResponse errorResponse)
        {
            if (!UnityIpcRequestCodec.TryDecodePlayEnterRequest(
                    request,
                    out enterRequest,
                    out errorResponse))
            {
                if (logDecodeFailure)
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Health,
                        "Play enter payload decode failed.");
                }

                return false;
            }

            errorResponse = null;
            return true;
        }

    }
}
