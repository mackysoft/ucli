using System;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Adapts the Play Mode exit IPC method to its typed lifecycle execution
    /// owner.
    /// </summary>
    internal sealed class PlayExitUnityIpcMethodHandler :
        IUnityIpcMethodHandler
    {
        private readonly IPlayExitLifecycleExecutionHandler executionHandler;
        private readonly IDaemonLogger daemonLogger;

        public PlayExitUnityIpcMethodHandler (
            IPlayExitLifecycleExecutionHandler executionHandler,
            IDaemonLogger daemonLogger)
        {
            this.executionHandler = executionHandler
                ?? throw new ArgumentNullException(nameof(executionHandler));
            this.daemonLogger = daemonLogger
                ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        public UnityIpcMethod Method => UnityIpcMethod.PlayExit;

        public async ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodePlayExitRequest(
                    request,
                    out var exitRequest,
                    out var decodeErrorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Health,
                    "Play exit payload decode failed.");
                return decodeErrorResponse;
            }

            var outcome = await executionHandler.ExecuteAsync(
                exitRequest.Start);
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
