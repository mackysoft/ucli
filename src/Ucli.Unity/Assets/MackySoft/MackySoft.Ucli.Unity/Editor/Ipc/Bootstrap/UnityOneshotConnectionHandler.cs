using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Decorates one shared IPC connection handler and coordinates oneshot lifecycle after terminal responses. </summary>
    internal sealed class UnityOneshotConnectionHandler : IUnityIpcConnectionHandler
    {
        private readonly UnityIpcConnectionHandler innerConnectionHandler;

        private readonly OneshotRequestCompletionSignal completionSignal;

        private readonly OneshotProcessLifetimeWatchdog lifetimeWatchdog;

        private readonly FileLifecycleExecutionStore executionStore;

        /// <summary> Initializes a new instance of the <see cref="UnityOneshotConnectionHandler" /> class. </summary>
        /// <param name="innerConnectionHandler"> The shared IPC connection handler. </param>
        /// <param name="completionSignal"> The oneshot completion signal. </param>
        /// <param name="lifetimeWatchdog"> The process watchdog that owns the bootstrap hard-exit deadline. </param>
        public UnityOneshotConnectionHandler (
            UnityIpcConnectionHandler innerConnectionHandler,
            OneshotRequestCompletionSignal completionSignal,
            OneshotProcessLifetimeWatchdog lifetimeWatchdog,
            FileLifecycleExecutionStore executionStore)
        {
            this.innerConnectionHandler = innerConnectionHandler ?? throw new ArgumentNullException(nameof(innerConnectionHandler));
            this.completionSignal = completionSignal ?? throw new ArgumentNullException(nameof(completionSignal));
            this.lifetimeWatchdog = lifetimeWatchdog ?? throw new ArgumentNullException(nameof(lifetimeWatchdog));
            this.executionStore = executionStore ?? throw new ArgumentNullException(nameof(executionStore));
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
                // A successful eval.plan is only the first half of one leased eval exchange.
                // Its terminal response must keep the oneshot host alive for eval.call.
                else if (result.Method != UnityIpcMethod.LifecycleStart
                    && result.Method != UnityIpcMethod.EvalPlan
                    && (result.Method != UnityIpcMethod.Shutdown
                        || result.IsShutdownAdmissionCommitted))
                {
                    if (!IsLifecycleAction(result.Method)
                        || await IsDurablyTerminalAsync(result.Request))
                    {
                        completionSignal.Signal();
                    }
                }
            }

            return result;
        }

        private async ValueTask<bool> IsDurablyTerminalAsync (
            ValidatedUnityIpcRequest request)
        {
            if (!TryGetLifecycleExecutionStart(
                    request,
                    out var executionKind,
                    out var start))
            {
                return false;
            }

            try
            {
                var stored = await executionStore.ReadAsync(
                    executionKind,
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None);
                if (stored?.IsTerminal != true)
                {
                    return false;
                }

                var expectedReference = start.LifecycleExecutionRef;
                var currentReference = stored.CurrentReference;
                return currentReference.Kind == expectedReference.Kind
                    && currentReference.Id == expectedReference.Id
                    && currentReference.DefinitionDigest
                        == expectedReference.DefinitionDigest
                    && currentReference.StatusLocator
                        == expectedReference.StatusLocator;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool TryGetLifecycleExecutionStart (
            ValidatedUnityIpcRequest request,
            out LifecycleExecutionKind executionKind,
            out LifecycleExecutionStartBinding start)
        {
            executionKind = default;
            start = null;
            switch (request.Method)
            {
                case UnityIpcMethod.Refresh:
                    executionKind = LifecycleExecutionKind.Refresh;
                    if (UnityIpcRequestCodec.TryDecodeRefreshRequest(
                            request,
                            out var refresh,
                            out _))
                    {
                        start = refresh.Start;
                    }
                    break;
                case UnityIpcMethod.Compile:
                    executionKind = LifecycleExecutionKind.Compile;
                    if (UnityIpcRequestCodec.TryDecodeCompileRequest(
                            request,
                            out var compile,
                            out _))
                    {
                        start = compile.Start;
                    }
                    break;
                case UnityIpcMethod.PlayEnter:
                    executionKind = LifecycleExecutionKind.PlayEnter;
                    if (UnityIpcRequestCodec.TryDecodePlayEnterRequest(
                            request,
                            out var playEnter,
                            out _))
                    {
                        start = playEnter.Start;
                    }
                    break;
                case UnityIpcMethod.PlayExit:
                    executionKind = LifecycleExecutionKind.PlayExit;
                    if (UnityIpcRequestCodec.TryDecodePlayExitRequest(
                            request,
                            out var playExit,
                            out _))
                    {
                        start = playExit.Start;
                    }
                    break;
            }

            return start != null;
        }

        private static bool IsLifecycleAction (UnityIpcMethod? method)
        {
            return method is UnityIpcMethod.Refresh
                or UnityIpcMethod.Compile
                or UnityIpcMethod.PlayEnter
                or UnityIpcMethod.PlayExit;
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
