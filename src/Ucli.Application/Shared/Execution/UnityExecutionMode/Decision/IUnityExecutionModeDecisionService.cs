using MackySoft.Ucli.Application.Shared.Context.Project;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

/// <summary> Resolves execution target and contract errors from one <c>--mode</c> input value. </summary>
internal interface IUnityExecutionModeDecisionService
{
    /// <summary> Resolves mode decision for the specified project and mode input. </summary>
    /// <param name="mode"> The normalized requested execution mode. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The remaining timeout budget available for daemon reachability probing. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mode decision result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    ValueTask<UnityExecutionModeDecisionResult> Decide (
        UnityExecutionMode mode,
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
