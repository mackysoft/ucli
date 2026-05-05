using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using MackySoft.Ucli.UnityIntegration.Project.Plugin;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Executes one IPC request through the resolved Unity daemon or oneshot host. </summary>
internal sealed class UnityIpcRequestExecutor : IUnityRequestExecutor
{
    private readonly IUnityExecutionModeDecisionService modeDecisionService;

    private readonly IUnityIpcClient daemonIpcClient;

    private readonly IUnityIpcClient oneshotIpcClient;

    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IUnityUcliPluginLocator unityUcliPluginLocator;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcRequestExecutor" /> class. </summary>
    /// <param name="modeDecisionService"> The Unity execution-mode decision service dependency. </param>
    /// <param name="unityIpcClients"> The Unity IPC client implementations grouped by execution target. </param>
    public UnityIpcRequestExecutor (
        IUnityExecutionModeDecisionService modeDecisionService,
        IDaemonPingInfoClient daemonPingInfoClient,
        IUnityUcliPluginLocator unityUcliPluginLocator,
        IEnumerable<IUnityIpcClient> unityIpcClients,
        TimeProvider? timeProvider = null)
    {
        this.modeDecisionService = modeDecisionService ?? throw new ArgumentNullException(nameof(modeDecisionService));
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.unityUcliPluginLocator = unityUcliPluginLocator ?? throw new ArgumentNullException(nameof(unityUcliPluginLocator));
        ArgumentNullException.ThrowIfNull(unityIpcClients);
        daemonIpcClient = ResolveRequiredClient<UnityDaemonIpcClient>(unityIpcClients);
        oneshotIpcClient = ResolveRequiredClient<UnityOneshotIpcClient>(unityIpcClients);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> Execute (
        UcliCommand command,
        MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode mode,
        TimeSpan timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        UnityRequestPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!command.IsValid)
        {
            throw new ArgumentException("Command name is invalid.", nameof(command));
        }

        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var ipcRequest = UnityIpcRequestPayloadFactory.Create(payload);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (!deadline.TryGetRemainingTimeout(out var modeDecisionTimeout))
        {
            return UnityRequestExecutionResult.Failure(
                "Timed out before Unity execution mode decision could begin.",
                ExecutionErrorCodeMapper.ToCode(ExecutionErrorKind.Timeout));
        }

        var modeDecisionResult = await modeDecisionService.Decide(
                mode,
                unityProject,
                modeDecisionTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (modeDecisionResult.HasContractError)
        {
            if (string.Equals(
                    modeDecisionResult.ContractError!.Code,
                    UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                    StringComparison.Ordinal))
            {
                var daemonModePluginLocateResult = await VerifyUnityPluginWithinBudget(
                        unityProject.UnityProjectRoot,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (daemonModePluginLocateResult != null)
                {
                    return UnityRequestExecutionResult.Failure(
                        daemonModePluginLocateResult.Message,
                        ExecutionErrorCodeMapper.ToCode(daemonModePluginLocateResult.Kind));
                }
            }

            return UnityRequestExecutionResult.Failure(
                modeDecisionResult.ContractError!.Message,
                modeDecisionResult.ContractError.Code);
        }

        if (!modeDecisionResult.IsSuccess)
        {
            return UnityRequestExecutionResult.Failure(
                modeDecisionResult.Error!.Message,
                ExecutionErrorCodeMapper.ToCode(modeDecisionResult.Error.Kind));
        }

        var decision = modeDecisionResult.Decision!;
        var opsReadRequest = TryParseOpsReadRequest(ipcRequest.Method, ipcRequest.Payload);
        if (decision.Target == UnityExecutionTarget.Oneshot)
        {
            var pluginLocateError = await VerifyUnityPluginWithinBudget(
                    unityProject.UnityProjectRoot,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (pluginLocateError != null)
            {
                return UnityRequestExecutionResult.Failure(
                    pluginLocateError.Message,
                    ExecutionErrorCodeMapper.ToCode(pluginLocateError.Kind));
            }
        }

        if (decision.Target == UnityExecutionTarget.Daemon
            && opsReadRequest is { RequireReadinessGate: true })
        {
            return await ExecuteDaemonOpsReadWithReadinessGate(
                    unityProject,
                    timeout,
                    deadline,
                    opsReadRequest,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
        {
            return UnityRequestExecutionResult.Failure(
                "Timed out before Unity IPC request dispatch could begin.",
                ExecutionErrorCodeMapper.ToCode(ExecutionErrorKind.Timeout));
        }

        var unityIpcClient = decision.Target switch
        {
            UnityExecutionTarget.Daemon => daemonIpcClient,
            UnityExecutionTarget.Oneshot => oneshotIpcClient,
            _ => throw new ArgumentOutOfRangeException(nameof(decision.Target), decision.Target, "Unsupported execution target."),
        };

        return await unityIpcClient.SendAsync(
                unityProject,
                ipcRequest.Method,
                ipcRequest.Payload,
                requestTimeout,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<UnityRequestExecutionResult> ExecuteDaemonOpsReadWithReadinessGate (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        ExecutionDeadline deadline,
        IpcOpsReadRequest opsReadRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);

        while (true)
        {
            var readinessResult = await WaitUntilDaemonReadiness(
                    unityProject,
                    opsReadRequest.FailFast,
                    deadline,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (readinessResult != null)
            {
                return readinessResult;
            }

            // NOTE:
            // Daemon-side ops.read readiness waits must not hold the shared IPC request loop open,
            // otherwise status/log/shutdown requests can be starved behind one long-lived wait.
            // Keep one final fail-fast gate on the dispatched request so the handler still rejects
            // lifecycle regressions that happen after the last client-side readiness probe.
            var payload = IpcPayloadCodec.SerializeToElement(opsReadRequest with
            {
                FailFast = true,
            });

            if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
            {
                return UnityRequestExecutionResult.Failure(
                    "Timed out before Unity IPC request dispatch could begin.",
                    ExecutionErrorCodeMapper.ToCode(ExecutionErrorKind.Timeout));
            }

            var dispatchResult = await daemonIpcClient.SendAsync(
                    unityProject,
                    IpcMethodNames.OpsRead,
                    payload,
                    requestTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!ShouldRetryDaemonOpsReadAfterLateWaitableRegression(dispatchResult, opsReadRequest.FailFast))
            {
                return dispatchResult;
            }
        }
    }

    private static IpcOpsReadRequest? TryParseOpsReadRequest (
        string method,
        JsonElement payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        if (!string.Equals(method, IpcMethodNames.OpsRead, StringComparison.Ordinal))
        {
            return null;
        }

        if (!IpcPayloadCodec.TryDeserialize(payload, out IpcOpsReadRequest parsedPayload, out _))
        {
            throw new InvalidOperationException("ops.read payload must be valid before Unity IPC request execution begins.");
        }

        return parsedPayload;
    }

    private async ValueTask<UnityRequestExecutionResult?> WaitUntilDaemonReadiness (
        ResolvedUnityProjectContext unityProject,
        bool failFast,
        ExecutionDeadline deadline,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return CreateDaemonTimeoutFailure(timeout);
            }

            var attemptTimeout = remainingTimeout < DaemonTimeouts.ProbeAttemptTimeoutCap
                ? remainingTimeout
                : DaemonTimeouts.ProbeAttemptTimeoutCap;
            try
            {
                var pingResponse = await daemonPingInfoClient.PingAndRead(
                        unityProject,
                        attemptTimeout,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                var readinessDecision = EvaluateDaemonReadiness(pingResponse, failFast);
                if (readinessDecision.IsReady)
                {
                    return null;
                }

                if (readinessDecision.IsFailure)
                {
                    return UnityRequestExecutionResult.Failure(
                        readinessDecision.ErrorMessage!,
                        readinessDecision.ErrorCode!);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException)
            {
            }
            catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
            {
                return UnityRequestExecutionResult.Failure(
                    $"Unity daemon is not running. {exception.Message}",
                    UnityExecutionModeDecisionErrorCodes.DaemonNotRunning);
            }
            catch (Exception exception)
            {
                return UnityRequestExecutionResult.Failure(
                    $"Failed while waiting for Unity daemon readiness. {exception.Message}",
                    IpcErrorCodes.InternalError);
            }

            if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
            {
                return CreateDaemonTimeoutFailure(timeout);
            }

            await TimeProviderDelay.Delay(
                    GetReadinessRetryDelay(remainingTimeout),
                    timeProvider,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static UnityRequestExecutionResult CreateDaemonTimeoutFailure (TimeSpan timeout)
    {
        return UnityRequestExecutionResult.Failure(
            $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.",
            ExecutionErrorCodes.IpcTimeout);
    }

    private static TimeSpan GetReadinessRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }

    private static DaemonReadinessDecision EvaluateDaemonReadiness (
        IpcPingResponse pingResponse,
        bool failFast)
    {
        ArgumentNullException.ThrowIfNull(pingResponse);

        if (pingResponse.CanAcceptExecutionRequests)
        {
            return DaemonReadinessDecision.Ready();
        }

        if (!IpcEditorLifecycleStateCodec.TryParse(pingResponse.LifecycleState, out var lifecycleState))
        {
            return DaemonReadinessDecision.Failure(
                IpcErrorCodes.InternalError,
                $"Unity editor lifecycle gate returned unsupported state '{pingResponse.LifecycleState}'.");
        }

        if (!failFast && IsWaitableLifecycleState(lifecycleState!))
        {
            return DaemonReadinessDecision.Wait();
        }

        return lifecycleState switch
        {
            IpcEditorLifecycleStateCodec.Starting => DaemonReadinessDecision.Failure(
                IpcErrorCodes.EditorStarting,
                "Unity editor startup is still in progress. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.Busy => DaemonReadinessDecision.Failure(
                IpcErrorCodes.EditorBusy,
                "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.Compiling => DaemonReadinessDecision.Failure(
                IpcErrorCodes.EditorCompiling,
                "Unity editor is compiling scripts. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.DomainReloading => DaemonReadinessDecision.Failure(
                IpcErrorCodes.EditorDomainReloading,
                "Unity editor is reloading the AppDomain. Retry after lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.Playmode => DaemonReadinessDecision.Failure(
                IpcErrorCodes.EditorPlaymode,
                "Unity editor is in Play Mode. Exit Play Mode and wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.BlockedByModal => DaemonReadinessDecision.Failure(
                IpcErrorCodes.EditorModalBlocked,
                "Unity editor is blocked by a modal dialog. Resolve the dialog and wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.SafeMode => DaemonReadinessDecision.Failure(
                IpcErrorCodes.EditorSafeMode,
                "Unity editor is in Safe Mode. Resolve compiler errors and wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.ShuttingDown => DaemonReadinessDecision.Failure(
                IpcErrorCodes.EditorShuttingDown,
                "Unity editor is shutting down and cannot accept execution requests."),
            _ => DaemonReadinessDecision.Failure(
                IpcErrorCodes.InternalError,
                $"Unity editor lifecycle gate returned unsupported state '{lifecycleState}'."),
        };
    }

    private static bool IsWaitableLifecycleState (string lifecycleState)
    {
        return string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Starting, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Busy, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Compiling, StringComparison.Ordinal);
    }

    private static bool ShouldRetryDaemonOpsReadAfterLateWaitableRegression (
        UnityRequestExecutionResult dispatchResult,
        bool failFast)
    {
        ArgumentNullException.ThrowIfNull(dispatchResult);

        if (failFast || !dispatchResult.IsSuccess)
        {
            return false;
        }

        if (!IpcResponseFailureReader.TryRead(dispatchResult.Response!, out var firstError, out _)
            || firstError == null)
        {
            return false;
        }

        return string.Equals(firstError.Code, IpcErrorCodes.EditorStarting, StringComparison.Ordinal)
            || string.Equals(firstError.Code, IpcErrorCodes.EditorBusy, StringComparison.Ordinal)
            || string.Equals(firstError.Code, IpcErrorCodes.EditorCompiling, StringComparison.Ordinal);
    }

    private async ValueTask<ExecutionError?> VerifyUnityPluginWithinBudget (
        string unityProjectRoot,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var timeout))
        {
            return ExecutionError.Timeout("Timed out before uCLI Unity plugin verification could begin.");
        }

        try
        {
            using var pluginLocateCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            pluginLocateCancellationTokenSource.CancelAfter(timeout);
            var pluginLocateResult = await unityUcliPluginLocator.Locate(
                    unityProjectRoot,
                    pluginLocateCancellationTokenSource.Token)
                .ConfigureAwait(false);
            return pluginLocateResult.IsSuccess
                ? null
                : pluginLocateResult.Error!;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ExecutionError.Timeout(
                $"Timed out while verifying the uCLI Unity plugin. Timeout={timeout.TotalMilliseconds:0}ms.");
        }
    }

    private static IUnityIpcClient ResolveRequiredClient<TClient> (IEnumerable<IUnityIpcClient> unityIpcClients)
        where TClient : class, IUnityIpcClient
    {
        IUnityIpcClient? resolvedClient = null;
        foreach (var unityIpcClient in unityIpcClients)
        {
            if (unityIpcClient is not TClient)
            {
                continue;
            }

            if (resolvedClient != null)
            {
                throw new InvalidOperationException($"Multiple Unity IPC clients were registered for '{typeof(TClient).Name}'.");
            }

            resolvedClient = unityIpcClient;
        }

        if (resolvedClient == null)
        {
            throw new InvalidOperationException($"Unity IPC client '{typeof(TClient).Name}' is not registered.");
        }

        return resolvedClient;
    }

    private readonly record struct DaemonReadinessDecision (
        bool IsReady,
        bool IsFailure,
        string? ErrorCode,
        string? ErrorMessage)
    {
        public static DaemonReadinessDecision Ready ()
        {
            return new DaemonReadinessDecision(
                IsReady: true,
                IsFailure: false,
                ErrorCode: null,
                ErrorMessage: null);
        }

        public static DaemonReadinessDecision Wait ()
        {
            return new DaemonReadinessDecision(
                IsReady: false,
                IsFailure: false,
                ErrorCode: null,
                ErrorMessage: null);
        }

        public static DaemonReadinessDecision Failure (
            string errorCode,
            string errorMessage)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
            return new DaemonReadinessDecision(
                IsReady: false,
                IsFailure: true,
                ErrorCode: errorCode,
                ErrorMessage: errorMessage);
        }
    }
}
