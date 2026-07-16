using System;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;

namespace MackySoft.Ucli.Unity.Tests
{
    /// <summary> Admits valid test envelopes before exposing validated requests to specialized handler stubs. </summary>
    internal abstract class ValidatedUnityIpcRequestHandlerStub : IUnityIpcRequestHandler
    {
        public virtual Task<UnityIpcRequestValidationResult> ValidateAsync (
            IpcRequestEnvelope request,
            IpcRequestPhaseScope phaseScope)
        {
            if (phaseScope == null)
            {
                throw new ArgumentNullException(nameof(phaseScope));
            }

            phaseScope.ExecutionCancellation.Token.ThrowIfCancellationRequested();
            return Task.FromResult(ValidatedUnityIpcRequestTestFactory.Success(request));
        }

        public virtual Task<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestPhaseScope phaseScope)
        {
            throw new NotSupportedException();
        }

        public virtual Task<IpcResponse> HandleStreamingAsync (
            ValidatedUnityIpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            IpcRequestPhaseScope phaseScope)
        {
            throw new NotSupportedException();
        }
    }
}
