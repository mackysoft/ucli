using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
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
    /// <param name="command"> The command that requested the mode decision. </param>
    /// <param name="mode"> The raw <c>--mode</c> option value. </param>
    /// <param name="timeout"> The raw <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="config"> The loaded configuration values. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mode decision result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="command" /> has an invalid name. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> or <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<UnityExecutionModeDecisionResult> Decide (
        UcliCommand command,
        string? mode,
        string? timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        if (!command.IsValid)
        {
            throw new ArgumentException("Command name is invalid.", nameof(command));
        }

        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        if (!UnityExecutionModeParser.TryParse(mode, out var requestedMode))
        {
            return UnityExecutionModeDecisionResultFactory.InvalidMode();
        }

        var timeoutResolutionResult = IpcCommandTimeoutResolver.Resolve(timeout, command, config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return UnityExecutionModeDecisionResultFactory.ProbeFailure(timeoutResolutionResult.Error!);
        }

        var reachabilityResult = await daemonReachabilityProbe.Probe(
                unityProject,
                timeoutResolutionResult.Timeout!.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (reachabilityResult.HasError)
        {
            return UnityExecutionModeDecisionResultFactory.ProbeFailure(reachabilityResult.Error!);
        }

        return UnityExecutionModeDecisionResultFactory.FromRequestedMode(
            requestedMode,
            reachabilityResult.IsRunning);
    }
}