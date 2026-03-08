using System.Text.Json;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Ipc;

/// <summary> Executes one IPC request through the resolved Unity daemon or oneshot host. </summary>
internal sealed class UnityIpcRequestExecutor : IUnityIpcRequestExecutor
{
    private const string OneshotSessionToken = "oneshot";

    private readonly IUnityExecutionModeDecisionService modeDecisionService;

    private readonly IUnityIpcClient unityIpcClient;

    private readonly IUnityOneshotIpcClient unityOneshotIpcClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcRequestExecutor" /> class. </summary>
    /// <param name="modeDecisionService"> The Unity execution-mode decision service dependency. </param>
    /// <param name="unityIpcClient"> The daemon IPC client dependency. </param>
    /// <param name="unityOneshotIpcClient"> The oneshot IPC client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session-token provider dependency. </param>
    public UnityIpcRequestExecutor (
        IUnityExecutionModeDecisionService modeDecisionService,
        IUnityIpcClient unityIpcClient,
        IUnityOneshotIpcClient unityOneshotIpcClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider)
    {
        this.modeDecisionService = modeDecisionService ?? throw new ArgumentNullException(nameof(modeDecisionService));
        this.unityIpcClient = unityIpcClient ?? throw new ArgumentNullException(nameof(unityIpcClient));
        this.unityOneshotIpcClient = unityOneshotIpcClient ?? throw new ArgumentNullException(nameof(unityOneshotIpcClient));
        this.daemonSessionTokenProvider = daemonSessionTokenProvider ?? throw new ArgumentNullException(nameof(daemonSessionTokenProvider));
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
        return decision.Target switch
        {
            UnityExecutionTarget.Daemon => await ExecuteDaemon(
                unityProject,
                decision.Timeout,
                method,
                payload,
                cancellationToken),
            UnityExecutionTarget.Oneshot => await ExecuteOneshot(
                unityProject,
                decision.Timeout,
                method,
                payload,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(decision.Target), decision.Target, "Unsupported execution target."),
        };
    }

    private async ValueTask<UnityIpcRequestExecutionResult> ExecuteDaemon (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string method,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var sessionTokenResult = await daemonSessionTokenProvider.Resolve(unityProject, cancellationToken).ConfigureAwait(false);
        if (!sessionTokenResult.IsSuccess)
        {
            var message = sessionTokenResult.IsSessionNotAvailable
                ? "Daemon session token is not available."
                : $"Daemon session token could not be resolved. {sessionTokenResult.Error!.Message}";
            return UnityIpcRequestExecutionResult.Failure(message, IpcErrorCodes.InternalError);
        }

        try
        {
            var response = await unityIpcClient.SendAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    CreateRequest(sessionTokenResult.Token!, method, payload),
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            return UnityIpcRequestExecutionResult.Success(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return UnityIpcRequestExecutionResult.Failure(
                $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                CliErrorCodes.IpcTimeout);
        }
        catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
        {
            return UnityIpcRequestExecutionResult.Failure(
                $"Unity daemon is not running. {exception.Message}",
                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning);
        }
        catch (Exception exception)
        {
            return UnityIpcRequestExecutionResult.Failure(
                $"Failed to execute Unity daemon IPC request. {exception.Message}",
                IpcErrorCodes.InternalError);
        }
    }

    private ValueTask<UnityIpcRequestExecutionResult> ExecuteOneshot (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string method,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        return unityOneshotIpcClient.SendAsync(
            unityProject.UnityProjectRoot,
            CreateRequest(OneshotSessionToken, method, payload),
            timeout,
            cancellationToken);
    }

    private static IpcRequest CreateRequest (
        string sessionToken,
        string method,
        JsonElement payload)
    {
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"{method}-{Guid.NewGuid():N}",
            SessionToken: sessionToken,
            Method: method,
            Payload: payload);
    }
}