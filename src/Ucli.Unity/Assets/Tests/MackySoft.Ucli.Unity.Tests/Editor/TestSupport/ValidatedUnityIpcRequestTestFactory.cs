using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Ipc;

namespace MackySoft.Ucli.Unity.Tests
{
    /// <summary> Creates endpoint-validated request models for tests that intentionally bypass authorization. </summary>
    internal static class ValidatedUnityIpcRequestTestFactory
    {
        public static ValidatedUnityIpcRequest Create (IpcRequestEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            if (!ContractLiteralCodec.TryParse(envelope.Method, out UnityIpcMethod method))
            {
                throw new ArgumentException("The test envelope must contain a defined Unity IPC method.", nameof(envelope));
            }

            if (!ContractLiteralCodec.TryParse(envelope.ResponseMode, out IpcResponseMode responseMode))
            {
                throw new ArgumentException("The test envelope must contain a defined IPC response mode.", nameof(envelope));
            }

            return Create(envelope, method, responseMode);
        }

        public static ValidatedUnityIpcRequest Create (
            IpcRequestEnvelope envelope,
            UnityIpcMethod method,
            IpcResponseMode responseMode)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            return new ValidatedUnityIpcRequest(
                envelope.RequestId,
                method,
                envelope.Payload,
                responseMode,
                envelope.RequestDeadlineUtc,
                envelope.RequestDeadlineRemainingMilliseconds);
        }

        public static UnityIpcRequestValidationResult Success (IpcRequestEnvelope envelope)
        {
            return UnityIpcRequestValidationResult.Success(Create(envelope));
        }
    }
}
