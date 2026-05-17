using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Decorates one shared IPC connection handler and signals oneshot completion after a terminal request. </summary>
    internal sealed class UnityOneshotConnectionHandler : IUnityIpcConnectionHandler
    {
        private readonly UnityIpcConnectionHandler innerConnectionHandler;

        private readonly OneshotRequestCompletionSignal completionSignal;

        /// <summary> Initializes a new instance of the <see cref="UnityOneshotConnectionHandler" /> class. </summary>
        /// <param name="innerConnectionHandler"> The shared IPC connection handler. </param>
        /// <param name="completionSignal"> The oneshot completion signal. </param>
        public UnityOneshotConnectionHandler (
            UnityIpcConnectionHandler innerConnectionHandler,
            OneshotRequestCompletionSignal completionSignal)
        {
            this.innerConnectionHandler = innerConnectionHandler ?? throw new ArgumentNullException(nameof(innerConnectionHandler));
            this.completionSignal = completionSignal ?? throw new ArgumentNullException(nameof(completionSignal));
        }

        /// <inheritdoc />
        public async Task<UnityIpcConnectionHandleResult> HandleAsync (
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            var result = await innerConnectionHandler.HandleAsync(stream, cancellationToken);
            if (ShouldSignalCompletion(result))
            {
                completionSignal.Signal();
            }

            return result;
        }

        private static bool ShouldSignalCompletion (UnityIpcConnectionHandleResult result)
        {
            if (result.Request == null || result.Response == null || HasPreDispatchFailure(result.Response))
            {
                return false;
            }

            return !string.Equals(result.Request.Method, IpcMethodNames.Ping, StringComparison.Ordinal);
        }

        private static bool HasPreDispatchFailure (IpcResponse response)
        {
            for (var i = 0; i < response.Errors.Count; i++)
            {
                if (IsPreDispatchErrorCode(response.Errors[i].Code))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPreDispatchErrorCode (UcliErrorCode errorCode)
        {
            return errorCode == IpcSessionErrorCodes.SessionTokenRequired
                || errorCode == IpcSessionErrorCodes.SessionTokenInvalid
                || errorCode == IpcProtocolErrorCodes.ProtocolVersionMismatch;
        }
    }
}
