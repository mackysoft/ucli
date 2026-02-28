using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution;

/// <summary> Resolves execution target and contract errors from one <c>--mode</c> input value. </summary>
internal interface IUnityExecutionModeDecisionService
{
    /// <summary> Resolves mode decision for the specified project and mode input. </summary>
    /// <param name="mode"> The raw <c>--mode</c> option value. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mode decision result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    ValueTask<UnityExecutionModeDecisionResult> DecideAsync (
        string? mode,
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
