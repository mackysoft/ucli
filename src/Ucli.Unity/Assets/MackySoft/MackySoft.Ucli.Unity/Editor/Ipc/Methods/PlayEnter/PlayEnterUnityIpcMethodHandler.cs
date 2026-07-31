using System;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Adapts the Play Mode enter IPC method to its typed lifecycle execution
    /// owner.
    /// </summary>
    internal sealed class PlayEnterUnityIpcMethodHandler :
        IUnityIpcMethodHandler
    {
        private readonly IPlayEnterLifecycleExecutionHandler executionHandler;
        private readonly IDaemonLogger daemonLogger;

        public PlayEnterUnityIpcMethodHandler (
            IPlayEnterLifecycleExecutionHandler executionHandler,
            IDaemonLogger daemonLogger)
        {
            this.executionHandler = executionHandler
                ?? throw new ArgumentNullException(nameof(executionHandler));
            this.daemonLogger = daemonLogger
                ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        public UnityIpcMethod Method => UnityIpcMethod.PlayEnter;

        public async ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodePlayEnterRequest(
                    request,
                    out var enterRequest,
                    out var decodeErrorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Health,
                    "Play enter payload decode failed.");
                return decodeErrorResponse;
            }

            var outcome = await executionHandler.ExecuteAsync(
                enterRequest.Start);
            if (outcome.IsSuccess)
            {
                return UnityIpcResponseFactory.CreateSuccessResponse(
                    request,
                    new IpcPlayTransitionResponse(
                        outcome.LifecycleExecutionRef,
                        outcome.Result));
            }

            if (!outcome.HasActionPayload)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    outcome.Error.Code,
                    outcome.Error.Message,
                    outcome.Error.InstancePath);
            }

            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                outcome.Error.Code,
                outcome.Error.Message,
                outcome.Error.InstancePath,
                new IpcPlayTransitionErrorResponse(
                    outcome.LifecycleExecutionRef,
                    outcome.ApplicationState,
                    outcome.Result));
        }
    }
}
