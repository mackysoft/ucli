using System;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Adapts the refresh IPC method to the typed refresh lifecycle execution
    /// owner.
    /// </summary>
    internal sealed class RefreshUnityIpcMethodHandler :
        IUnityIpcMethodHandler
    {
        private readonly IRefreshLifecycleExecutionHandler executionHandler;
        private readonly IDaemonLogger daemonLogger;

        public RefreshUnityIpcMethodHandler (
            IRefreshLifecycleExecutionHandler executionHandler,
            IDaemonLogger daemonLogger)
        {
            this.executionHandler = executionHandler
                ?? throw new ArgumentNullException(nameof(executionHandler));
            this.daemonLogger = daemonLogger
                ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        public UnityIpcMethod Method => UnityIpcMethod.Refresh;

        public async ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeRefreshRequest(
                    request,
                    out var refreshRequest,
                    out var decodeErrorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Refresh payload decode failed.");
                return decodeErrorResponse;
            }

            var outcome = await executionHandler.ExecuteAsync(
                refreshRequest.Start);
            if (outcome.IsSuccess)
            {
                return UnityIpcResponseFactory.CreateSuccessResponse(
                    request,
                    new IpcRefreshResponse(
                        outcome.Project,
                        (TerminalExecutionRef)outcome.LifecycleExecutionRef,
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
                new IpcRefreshErrorResponse(
                    outcome.Project,
                    outcome.LifecycleExecutionRef,
                    outcome.ApplicationState,
                    outcome.Refresh,
                    outcome.ObservedLifecycle,
                    outcome.ReadPostcondition));
        }
    }
}
