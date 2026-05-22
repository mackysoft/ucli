using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Cryptography;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>play.enter</c> IPC method requests. </summary>
    internal sealed class PlayEnterUnityIpcMethodHandler : IRecoverableUnityIpcMethodHandler
    {
        private readonly PlayEnterTransitionRunner transitionRunner;
        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="PlayEnterUnityIpcMethodHandler" /> class. </summary>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <param name="readinessGate"> The lifecycle snapshot provider dependency. </param>
        /// <param name="projectIdentity"> The project identity served by this IPC host. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        public PlayEnterUnityIpcMethodHandler (
            IServerVersionProvider serverVersionProvider,
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            IDaemonLogger daemonLogger = null)
            : this(
                new PlayEnterTransitionRunner(serverVersionProvider, readinessGate, projectIdentity, daemonLogger),
                daemonLogger)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="PlayEnterUnityIpcMethodHandler" /> class. </summary>
        /// <param name="transitionRunner"> The transition runner dependency. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        internal PlayEnterUnityIpcMethodHandler (
            PlayEnterTransitionRunner transitionRunner,
            IDaemonLogger daemonLogger = null)
        {
            this.transitionRunner = transitionRunner ?? throw new ArgumentNullException(nameof(transitionRunner));
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.PlayEnter;

        /// <inheritdoc />
        public bool TryCreateRecoverableRequestPayloadHash (
            IpcRequest request,
            out string requestPayloadHash,
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

            if (!TryReadPlayEnterRequest(
                    request,
                    logDecodeFailure: true,
                    out IpcPlayEnterRequest enterRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var result = await transitionRunner.EnterAsync(
                enterRequest.TimeoutMilliseconds!.Value,
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

        private bool TryReadPlayEnterRequest (
            IpcRequest request,
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

            if (!TryValidateTimeoutMilliseconds(enterRequest!.TimeoutMilliseconds, out var timeoutErrorMessage))
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
                errorMessage = "PlayEnter timeoutMilliseconds is required.";
                return false;
            }

            if (timeoutMilliseconds.Value <= 0)
            {
                errorMessage = "PlayEnter timeoutMilliseconds must be greater than zero.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
