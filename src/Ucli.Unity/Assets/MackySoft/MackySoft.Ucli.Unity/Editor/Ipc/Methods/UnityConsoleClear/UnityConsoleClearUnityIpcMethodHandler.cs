using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>unity.console.clear</c> IPC method requests. </summary>
    internal sealed class UnityConsoleClearUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private const int RequestedByMaxLength = 64;

        private readonly IUnityConsoleClearer unityConsoleClearer;

        private readonly IUnityEditorReadinessGate readinessGate;

        private readonly IDaemonLogger daemonLogger;

        private readonly IUnityMutationLaneControl mutationLaneControl;

        /// <summary> Initializes a new instance of the <see cref="UnityConsoleClearUnityIpcMethodHandler" /> class. </summary>
        /// <param name="unityConsoleClearer"> The Unity Console clear adapter dependency. </param>
        /// <param name="readinessGate"> The editor-readiness gate dependency. </param>
        /// <param name="daemonLogger"> The daemon daemon-logger dependency. </param>
        public UnityConsoleClearUnityIpcMethodHandler (
            IUnityConsoleClearer unityConsoleClearer,
            IUnityEditorReadinessGate readinessGate,
            IDaemonLogger daemonLogger,
            IUnityMutationLaneControl mutationLaneControl)
        {
            this.unityConsoleClearer = unityConsoleClearer ?? throw new ArgumentNullException(nameof(unityConsoleClearer));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
            this.mutationLaneControl = mutationLaneControl ?? throw new ArgumentNullException(nameof(mutationLaneControl));
        }

        /// <inheritdoc />
        public UnityIpcMethod Method => UnityIpcMethod.UnityConsoleClear;

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

            if (!UnityIpcRequestCodec.TryDecodeUnityConsoleClearRequest(
                    request,
                    out IpcUnityConsoleClearRequest payload,
                    out var decodeErrorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Unity Console clear payload decode failed.",
                    decodeErrorResponse.Errors.Count > 0 ? decodeErrorResponse.Errors[0].Message : null);
                return new ValueTask<IpcResponse>(decodeErrorResponse);
            }

            if (!TryValidateRequestedBy(payload.RequestedBy, out var requestedByErrorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Unity Console clear rejected due to invalid request.",
                    requestedByErrorMessage);
                return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    requestedByErrorMessage,
                    null));
            }

            var snapshot = readinessGate.CaptureObservation();
            if (snapshot.State.EditorMode != DaemonEditorMode.Gui)
            {
                var message = "Unity Console clear requires a GUI Unity Editor daemon session.";
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Unity Console clear rejected due to editor mode.",
                    message);
                return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    message,
                    null));
            }

            var mutationActivity = mutationLaneControl.BeginMutation();
            UnityConsoleClearResult clearResult;
            try
            {
                clearResult = unityConsoleClearer.Clear();
            }
            finally
            {
                mutationActivity.Complete();
            }
            if (!clearResult.IsSuccess)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Unity Console clear failed.",
                    clearResult.ErrorMessage);
                return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    clearResult.ErrorMessage,
                    null));
            }

            daemonLogger.Info(
                DaemonLogCategories.Ipc,
                "Unity Console clear completed.",
                payload.RequestedBy);
            var responsePayload = new IpcUnityConsoleClearResponse();
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(request, responsePayload));
        }

        private static bool TryValidateRequestedBy (
            string requestedBy,
            out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(requestedBy))
            {
                errorMessage = "Unity Console clear request 'requestedBy' must not be empty.";
                return false;
            }

            if (requestedBy.Length > RequestedByMaxLength)
            {
                errorMessage = $"Unity Console clear request 'requestedBy' must be {RequestedByMaxLength} characters or less.";
                return false;
            }

            foreach (var value in requestedBy)
            {
                if (!IsAllowedRequestedByCharacter(value))
                {
                    errorMessage = "Unity Console clear request 'requestedBy' must contain only ASCII letters, digits, '.', '-' or '_'.";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool IsAllowedRequestedByCharacter (char value)
        {
            return (value >= 'a' && value <= 'z')
                || (value >= 'A' && value <= 'Z')
                || (value >= '0' && value <= '9')
                || value == '.'
                || value == '-'
                || value == '_';
        }
    }
}
