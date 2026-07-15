using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Decorates one shared IPC connection handler and coordinates oneshot lifecycle after terminal responses. </summary>
    internal sealed class UnityOneshotConnectionHandler : IUnityIpcConnectionHandler
    {
        private readonly UnityIpcConnectionHandler innerConnectionHandler;

        private readonly OneshotRequestCompletionSignal completionSignal;

        private readonly OneshotProcessLifetimeWatchdog lifetimeWatchdog;

        /// <summary> Initializes a new instance of the <see cref="UnityOneshotConnectionHandler" /> class. </summary>
        /// <param name="innerConnectionHandler"> The shared IPC connection handler. </param>
        /// <param name="completionSignal"> The oneshot completion signal. </param>
        /// <param name="lifetimeWatchdog"> The process watchdog that owns the original request deadline. </param>
        public UnityOneshotConnectionHandler (
            UnityIpcConnectionHandler innerConnectionHandler,
            OneshotRequestCompletionSignal completionSignal,
            OneshotProcessLifetimeWatchdog lifetimeWatchdog)
        {
            this.innerConnectionHandler = innerConnectionHandler ?? throw new ArgumentNullException(nameof(innerConnectionHandler));
            this.completionSignal = completionSignal ?? throw new ArgumentNullException(nameof(completionSignal));
            this.lifetimeWatchdog = lifetimeWatchdog ?? throw new ArgumentNullException(nameof(lifetimeWatchdog));
        }

        /// <inheritdoc />
        public async Task<UnityIpcConnectionHandleResult> HandleAsync (
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            var result = await innerConnectionHandler.HandleAsync(stream, cancellationToken);
            if (result != null
                && result.HasTerminalResponse
                && !HasPreDispatchFailure(result.Response!))
            {
                if (result.Method == UnityIpcMethod.Ping)
                {
                    if (!IsOneshotStartupPing(result.Request!))
                    {
                        // The CLI owns a separate cleanup deadline after the terminal command response.
                        // Keep only parent-process monitoring while it retries the shutdown exchange.
                        lifetimeWatchdog.MarkRequestCompleted();
                    }
                }
                else if (result.Method != UnityIpcMethod.Shutdown
                    || result.IsShutdownAdmissionCommitted)
                {
                    completionSignal.Signal();
                }
            }

            return result;
        }

        private static bool IsOneshotStartupPing (ValidatedUnityIpcRequest request)
        {
            return UnityIpcRequestCodec.TryDecodePingRequest(
                    request,
                    out var payload,
                    out _)
                && payload != null
                && string.Equals(
                    payload.ClientVersion,
                    IpcPingClientVersions.OneshotStartup,
                    StringComparison.Ordinal);
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

        private static bool IsPreDispatchErrorCode (UcliCode errorCode)
        {
            return errorCode == IpcSessionErrorCodes.SessionTokenRequired
                || errorCode == IpcSessionErrorCodes.SessionTokenInvalid
                || errorCode == IpcProtocolErrorCodes.ProtocolVersionMismatch;
        }
    }
}
