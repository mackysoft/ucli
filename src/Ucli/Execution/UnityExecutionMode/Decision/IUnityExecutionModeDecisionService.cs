using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution;

/// <summary> Resolves execution target and contract errors from one <c>--mode</c> input value. </summary>
internal interface IUnityExecutionModeDecisionService
{
    /// <summary> Resolves mode decision for the specified project and mode input. </summary>
    /// <param name="command"> The command that requested the mode decision. </param>
    /// <param name="mode"> The raw <c>--mode</c> option value. </param>
    /// <param name="timeout"> The raw <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="config"> The loaded configuration values. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mode decision result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="command" /> has an invalid name. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> or <paramref name="unityProject" /> is <see langword="null" />. </exception>
    ValueTask<UnityExecutionModeDecisionResult> Decide (
        UcliCommand command,
        string? mode,
        string? timeout,
        UcliConfig config,
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}