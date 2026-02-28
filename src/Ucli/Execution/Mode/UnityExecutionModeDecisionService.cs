using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution;

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
            return UnityExecutionModeDecisionResultFactory.InvalidMode();
        }

        var reachabilityResult = await daemonReachabilityProbe.Probe(unityProject, cancellationToken).ConfigureAwait(false);
        if (reachabilityResult.HasError)
        {
            return UnityExecutionModeDecisionResultFactory.ProbeFailure(reachabilityResult.Error!);
        }

        return UnityExecutionModeDecisionResultFactory.FromRequestedMode(
            requestedMode,
            reachabilityResult.IsRunning);
    }
}