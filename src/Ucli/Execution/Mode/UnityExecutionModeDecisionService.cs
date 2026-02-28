using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution;

/// <summary> Implements mode decision based on requested mode and daemon reachability. </summary>
internal sealed class UnityExecutionModeDecisionService : IUnityExecutionModeDecisionService
{
    private const string InvalidModeMessage = "Mode must be auto, daemon, or oneshot.";

    private const string DaemonNotRunningMessage = "Daemon is not running for mode=daemon.";

    private const string DaemonRunningOneshotForbiddenMessage = "Daemon is running for mode=oneshot.";

    private readonly IDaemonReachabilityProbe daemonReachabilityProbe;

    /// <summary> Initializes a new instance of the <see cref="UnityExecutionModeDecisionService" /> class. </summary>
    /// <param name="daemonReachabilityProbe"> The daemon reachability probe dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonReachabilityProbe" /> is <see langword="null" />. </exception>
    public UnityExecutionModeDecisionService (IDaemonReachabilityProbe daemonReachabilityProbe)
    {
        this.daemonReachabilityProbe = daemonReachabilityProbe ?? throw new ArgumentNullException(nameof(daemonReachabilityProbe));
    }

    /// <summary> Resolves execution target and contract errors for one requested mode. </summary>
    /// <param name="mode"> The raw <c>--mode</c> option value. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mode decision result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<UnityExecutionModeDecisionResult> Decide (
        string? mode,
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        if (!UnityExecutionModeParser.TryParse(mode, out var requestedMode))
        {
            return UnityExecutionModeDecisionResult.Failure(ExecutionError.InvalidArgument(InvalidModeMessage));
        }

        var reachabilityResult = await daemonReachabilityProbe.Probe(unityProject, cancellationToken).ConfigureAwait(false);
        if (reachabilityResult.HasError)
        {
            return UnityExecutionModeDecisionResult.Failure(reachabilityResult.Error!);
        }

        if (requestedMode == UnityExecutionMode.Daemon && !reachabilityResult.IsRunning)
        {
            return UnityExecutionModeDecisionResult.ContractFailure(new ModeDecisionContractError(
                ModeDecisionErrorCodes.DaemonNotRunning,
                DaemonNotRunningMessage));
        }

        if (requestedMode == UnityExecutionMode.Oneshot && reachabilityResult.IsRunning)
        {
            return UnityExecutionModeDecisionResult.ContractFailure(new ModeDecisionContractError(
                ModeDecisionErrorCodes.DaemonRunningOneshotForbidden,
                DaemonRunningOneshotForbiddenMessage));
        }

        var target = ResolveTarget(requestedMode, reachabilityResult.IsRunning);
        var decision = new UnityExecutionModeDecision(requestedMode, reachabilityResult.IsRunning, target);
        return UnityExecutionModeDecisionResult.Success(decision);
    }

    /// <summary> Resolves execution target from requested mode and daemon reachability. </summary>
    /// <param name="requestedMode"> The requested mode. </param>
    /// <param name="daemonRunning"> Whether daemon is reachable. </param>
    /// <returns> The resolved execution target. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="requestedMode" /> is outside supported values. </exception>
    private static UnityExecutionTarget ResolveTarget (
        UnityExecutionMode requestedMode,
        bool daemonRunning)
    {
        return requestedMode switch
        {
            UnityExecutionMode.Auto when daemonRunning => UnityExecutionTarget.Daemon,
            UnityExecutionMode.Auto => UnityExecutionTarget.Oneshot,
            UnityExecutionMode.Daemon => UnityExecutionTarget.Daemon,
            UnityExecutionMode.Oneshot => UnityExecutionTarget.Oneshot,
            _ => throw new ArgumentOutOfRangeException(nameof(requestedMode), requestedMode, "Unsupported execution mode."),
        };
    }
}
