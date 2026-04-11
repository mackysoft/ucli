using System.Text.Json;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Ipc;

/// <summary> Executes one IPC request through the resolved Unity daemon or oneshot host. </summary>
internal sealed class UnityIpcRequestExecutor : IUnityIpcRequestExecutor
{
    private readonly IUnityExecutionModeDecisionService modeDecisionService;

    private readonly IUnityIpcClient daemonIpcClient;

    private readonly IUnityIpcClient oneshotIpcClient;

    private readonly IUnityUcliPluginLocator unityUcliPluginLocator;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcRequestExecutor" /> class. </summary>
    /// <param name="modeDecisionService"> The Unity execution-mode decision service dependency. </param>
    /// <param name="unityIpcClients"> The Unity IPC client implementations grouped by execution target. </param>
    public UnityIpcRequestExecutor (
        IUnityExecutionModeDecisionService modeDecisionService,
        IUnityUcliPluginLocator unityUcliPluginLocator,
        IEnumerable<IUnityIpcClient> unityIpcClients,
        TimeProvider? timeProvider = null)
    {
        this.modeDecisionService = modeDecisionService ?? throw new ArgumentNullException(nameof(modeDecisionService));
        this.unityUcliPluginLocator = unityUcliPluginLocator ?? throw new ArgumentNullException(nameof(unityUcliPluginLocator));
        ArgumentNullException.ThrowIfNull(unityIpcClients);
        daemonIpcClient = ResolveRequiredClient<UnityDaemonIpcClient>(unityIpcClients);
        oneshotIpcClient = ResolveRequiredClient<UnityOneshotIpcClient>(unityIpcClients);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<UnityIpcRequestExecutionResult> Execute (
        UcliCommand command,
        string? mode,
        string? timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        string method,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        if (!command.IsValid)
        {
            throw new ArgumentException("Command name is invalid.", nameof(command));
        }

        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        cancellationToken.ThrowIfCancellationRequested();

        var timeoutResolutionResult = IpcCommandTimeoutResolver.Resolve(timeout, command, config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return UnityIpcRequestExecutionResult.Failure(
                timeoutResolutionResult.Error!.Message,
                ExecutionErrorKindCodeMapper.ToCode(timeoutResolutionResult.Error.Kind));
        }

        var deadline = ExecutionDeadline.Start(timeoutResolutionResult.Timeout!.Value, timeProvider);
        if (!deadline.TryGetRemainingTimeout(out var modeDecisionTimeout))
        {
            return UnityIpcRequestExecutionResult.Failure(
                "Timed out before Unity execution mode decision could begin.",
                ExecutionErrorKindCodeMapper.ToCode(ExecutionErrorKind.Timeout));
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
                    return UnityIpcRequestExecutionResult.Failure(
                        daemonModePluginLocateResult.Message,
                        ExecutionErrorKindCodeMapper.ToCode(daemonModePluginLocateResult.Kind));
                }
            }

            return UnityIpcRequestExecutionResult.Failure(
                modeDecisionResult.ContractError!.Message,
                modeDecisionResult.ContractError.Code);
        }

        if (!modeDecisionResult.IsSuccess)
        {
            return UnityIpcRequestExecutionResult.Failure(
                modeDecisionResult.Error!.Message,
                ExecutionErrorKindCodeMapper.ToCode(modeDecisionResult.Error.Kind));
        }

        var decision = modeDecisionResult.Decision!;
        if (decision.Target == UnityExecutionTarget.Oneshot)
        {
            var pluginLocateError = await VerifyUnityPluginWithinBudget(
                    unityProject.UnityProjectRoot,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (pluginLocateError != null)
            {
                return UnityIpcRequestExecutionResult.Failure(
                    pluginLocateError.Message,
                    ExecutionErrorKindCodeMapper.ToCode(pluginLocateError.Kind));
            }
        }

        if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
        {
            return UnityIpcRequestExecutionResult.Failure(
                "Timed out before Unity IPC request dispatch could begin.",
                ExecutionErrorKindCodeMapper.ToCode(ExecutionErrorKind.Timeout));
        }

        var unityIpcClient = decision.Target switch
        {
            UnityExecutionTarget.Daemon => daemonIpcClient,
            UnityExecutionTarget.Oneshot => oneshotIpcClient,
            _ => throw new ArgumentOutOfRangeException(nameof(decision.Target), decision.Target, "Unsupported execution target."),
        };

        return await unityIpcClient.SendAsync(
                unityProject,
                method,
                payload,
                requestTimeout,
                cancellationToken)
            .ConfigureAwait(false);
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
}