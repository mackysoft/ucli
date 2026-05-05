namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

/// <summary> Implements mode decision based on requested mode and daemon reachability. </summary>
internal sealed class UnityExecutionModeDecisionService : IUnityExecutionModeDecisionService
{
    private readonly IDaemonReachabilityProbe daemonReachabilityProbe;

    /// <summary> Initializes a new instance of the <see cref="UnityExecutionModeDecisionService" /> class. </summary>
    /// <param name="daemonReachabilityProbe"> The daemon reachability probe dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonReachabilityProbe" /> is <see langword="null" />. </exception>
    public UnityExecutionModeDecisionService (IDaemonReachabilityProbe daemonReachabilityProbe)
    {
        this.daemonReachabilityProbe = daemonReachabilityProbe ?? throw new ArgumentNullException(nameof(daemonReachabilityProbe));
    }

    /// <summary> Resolves execution target and contract errors for one requested mode. </summary>
    /// <param name="mode"> The normalized requested execution mode. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The remaining timeout budget available for daemon reachability probing. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mode decision result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<UnityExecutionModeDecisionResult> Decide (
        UnityExecutionMode mode,
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var reachabilityResult = await daemonReachabilityProbe.Probe(
                unityProject,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (reachabilityResult.HasError)
        {
            return UnityExecutionModeDecisionResultFactory.ProbeFailure(reachabilityResult.Error!);
        }

        return UnityExecutionModeDecisionResultFactory.FromRequestedMode(
            mode,
            reachabilityResult.IsRunning,
            timeout);
    }
}
