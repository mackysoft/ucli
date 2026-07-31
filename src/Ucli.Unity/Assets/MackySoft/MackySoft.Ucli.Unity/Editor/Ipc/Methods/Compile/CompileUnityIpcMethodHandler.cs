using System;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Adapts the compile IPC method to the typed compile lifecycle execution
    /// owner.
    /// </summary>
    internal sealed class CompileUnityIpcMethodHandler :
        IUnityIpcMethodHandler
    {
        private readonly ICompileLifecycleExecutionHandler executionHandler;
        private readonly IDaemonLogger daemonLogger;

        public CompileUnityIpcMethodHandler (
            ICompileLifecycleExecutionHandler executionHandler,
            IDaemonLogger daemonLogger)
        {
            this.executionHandler = executionHandler
                ?? throw new ArgumentNullException(nameof(executionHandler));
            this.daemonLogger = daemonLogger
                ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        public UnityIpcMethod Method => UnityIpcMethod.Compile;

        public async ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeCompileRequest(
                    request,
                    out var compileRequest,
                    out var decodeErrorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Compile payload decode failed.");
                return decodeErrorResponse;
            }

            var outcome = await executionHandler.ExecuteAsync(
                compileRequest.Start);
            if (outcome.IsSuccess)
            {
                return UnityIpcResponseFactory.CreateSuccessResponse(
                    request,
                    new IpcCompileResponse(
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
                new IpcCompileErrorResponse(
                    outcome.LifecycleExecutionRef,
                    outcome.ApplicationState,
                    outcome.Result,
                    outcome.ObservedLifecycle));
        }
    }
}
