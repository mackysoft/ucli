using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Resolves the Unity IPC execution target and performs pre-dispatch target checks. </summary>
internal sealed class UnityIpcExecutionTargetResolver
{
    private readonly IUnityExecutionModeDecisionService modeDecisionService;

    private readonly UnityIpcPluginVerifier pluginVerifier;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcExecutionTargetResolver" /> class. </summary>
    /// <param name="modeDecisionService"> The Unity execution mode decision dependency. </param>
    /// <param name="pluginVerifier"> The Unity plugin verifier dependency. </param>
    public UnityIpcExecutionTargetResolver (
        IUnityExecutionModeDecisionService modeDecisionService,
        UnityIpcPluginVerifier pluginVerifier)
    {
        this.modeDecisionService = modeDecisionService ?? throw new ArgumentNullException(nameof(modeDecisionService));
        this.pluginVerifier = pluginVerifier ?? throw new ArgumentNullException(nameof(pluginVerifier));
    }

    /// <summary> Resolves the execution target within the shared timeout budget. </summary>
    /// <param name="mode"> The requested execution mode. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="budget"> The shared execution timeout budget. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The resolved target, or a classified failure. </returns>
    public async ValueTask<UnityIpcExecutionTargetResolutionResult> ResolveAsync (
        UnityExecutionMode mode,
        ResolvedUnityProjectContext unityProject,
        UnityIpcExecutionBudget budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(budget);
        cancellationToken.ThrowIfCancellationRequested();

        if (mode == UnityExecutionMode.Daemon)
        {
            return UnityIpcExecutionTargetResolutionResult.Success(UnityExecutionTarget.Daemon);
        }

        if (!budget.TryGetRemainingTimeout(out var modeDecisionTimeout))
        {
            return UnityIpcExecutionTargetResolutionResult.FailureResult(UnityIpcFailureClassifier.Timeout(
                "Timed out before Unity execution mode decision could begin."));
        }

        UnityExecutionModeDecisionResult modeDecisionResult;
        try
        {
            modeDecisionResult = await modeDecisionService.DecideAsync(
                    mode,
                    unityProject,
                    modeDecisionTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return UnityIpcExecutionTargetResolutionResult.FailureResult(
                UnityIpcFailureClassifier.FromExecutionError(ExecutionError.InternalError(
                    $"Failed to decide Unity execution mode. {exception.Message}")));
        }

        if (modeDecisionResult.HasContractError)
        {
            return await ResolveContractFailureAsync(
                    modeDecisionResult.ContractError!,
                    unityProject,
                    budget,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!modeDecisionResult.IsSuccess)
        {
            return UnityIpcExecutionTargetResolutionResult.FailureResult(
                UnityIpcFailureClassifier.FromExecutionError(modeDecisionResult.Error!));
        }

        var decision = modeDecisionResult.Decision!;
        if (decision.Target == UnityExecutionTarget.Oneshot)
        {
            var pluginFailure = await pluginVerifier.VerifyWithinBudgetAsync(
                    unityProject.UnityProjectRoot,
                    budget,
                    cancellationToken)
                .ConfigureAwait(false);
            if (pluginFailure != null)
            {
                return UnityIpcExecutionTargetResolutionResult.FailureResult(pluginFailure);
            }
        }

        return UnityIpcExecutionTargetResolutionResult.Success(decision.Target);
    }

    private async ValueTask<UnityIpcExecutionTargetResolutionResult> ResolveContractFailureAsync (
        UnityExecutionModeDecisionContractError contractError,
        ResolvedUnityProjectContext unityProject,
        UnityIpcExecutionBudget budget,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                contractError.Code,
                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                StringComparison.Ordinal))
        {
            var pluginFailure = await pluginVerifier.VerifyWithinBudgetAsync(
                    unityProject.UnityProjectRoot,
                    budget,
                    cancellationToken)
                .ConfigureAwait(false);
            if (pluginFailure != null)
            {
                return UnityIpcExecutionTargetResolutionResult.FailureResult(pluginFailure);
            }
        }

        return UnityIpcExecutionTargetResolutionResult.FailureResult(
            UnityIpcFailureClassifier.FromModeDecisionContractError(contractError));
    }
}
