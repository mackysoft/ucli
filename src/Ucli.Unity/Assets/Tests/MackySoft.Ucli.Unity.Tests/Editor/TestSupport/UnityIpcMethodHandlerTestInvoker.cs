using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;

namespace MackySoft.Ucli.Unity.Tests
{
    /// <summary> Invokes IPC method handlers with a connection-owned phase scope. </summary>
    internal static class UnityIpcMethodHandlerTestInvoker
    {
        public static async ValueTask<IpcResponse> HandleAsync (
            IUnityIpcMethodHandler handler,
            IpcRequestEnvelope request,
            CancellationToken cancellationToken = default)
        {
            using var phaseScope = new IpcRequestPhaseScopeFactory().Create(
                request,
                cancellationToken,
                TimeSpan.FromSeconds(1));
            return await handler.HandleAsync(
                CreateValidatedRequest(request, handler.Method, IpcResponseMode.Single),
                phaseScope.ExecutionCancellation);
        }

        public static async ValueTask<IpcResponse> HandleStreamingAsync (
            IStreamingUnityIpcMethodHandler handler,
            IpcRequestEnvelope request,
            IIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken = default)
        {
            using var phaseScope = new IpcRequestPhaseScopeFactory().Create(
                request,
                cancellationToken,
                TimeSpan.FromSeconds(1));
            return await handler.HandleStreamingAsync(
                CreateValidatedRequest(request, handler.Method, IpcResponseMode.Stream),
                streamWriter,
                phaseScope.ExecutionCancellation);
        }

        public static async ValueTask<IpcResponse> HandleRecoverableAsync (
            IRecoverableUnityIpcMethodHandler handler,
            IpcRequestEnvelope request,
            RecoverableIpcOperationContext context,
            CancellationToken cancellationToken = default)
        {
            using var phaseScope = new IpcRequestPhaseScopeFactory().Create(
                request,
                cancellationToken,
                TimeSpan.FromSeconds(1));
            return await handler.HandleRecoverableAsync(
                CreateValidatedRequest(request, handler.Method, IpcResponseMode.Single),
                context,
                phaseScope.ExecutionCancellation);
        }

        private static ValidatedUnityIpcRequest CreateValidatedRequest (
            IpcRequestEnvelope request,
            UnityIpcMethod method,
            IpcResponseMode responseMode)
        {
            return ValidatedUnityIpcRequestTestFactory.Create(request, method, responseMode);
        }
    }
}
