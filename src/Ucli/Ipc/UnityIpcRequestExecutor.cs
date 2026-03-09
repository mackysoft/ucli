using System.Text.Json;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Ipc;

/// <summary> Executes one IPC request through the resolved Unity daemon or oneshot host. </summary>
internal sealed class UnityIpcRequestExecutor : IUnityIpcRequestExecutor
{
    private readonly IUnityExecutionModeDecisionService modeDecisionService;

    private readonly IUnityIpcClient daemonIpcClient;

    private readonly IUnityIpcClient oneshotIpcClient;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcRequestExecutor" /> class. </summary>
    /// <param name="modeDecisionService"> The Unity execution-mode decision service dependency. </param>
    /// <param name="unityIpcClients"> The Unity IPC client implementations grouped by execution target. </param>
    public UnityIpcRequestExecutor (
        IUnityExecutionModeDecisionService modeDecisionService,
        IEnumerable<IUnityIpcClient> unityIpcClients)
    {
        this.modeDecisionService = modeDecisionService ?? throw new ArgumentNullException(nameof(modeDecisionService));
        ArgumentNullException.ThrowIfNull(unityIpcClients);
        daemonIpcClient = ResolveRequiredClient<UnityDaemonIpcClient>(unityIpcClients);
        oneshotIpcClient = ResolveRequiredClient<UnityOneshotIpcClient>(unityIpcClients);
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

        var modeDecisionResult = await modeDecisionService.Decide(
                command,
                mode,
                timeout,
                config,
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (modeDecisionResult.HasContractError)
        {
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
                decision.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
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