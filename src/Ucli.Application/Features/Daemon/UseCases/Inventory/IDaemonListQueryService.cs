namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;

/// <summary> Lists persisted daemon registrations across Git worktrees for one Unity project path. </summary>
internal interface IDaemonListQueryService
{
    /// <summary> Resolves daemon registrations across Git worktrees for one Unity project context. </summary>
    /// <param name="unityProject"> The current Unity project context. </param>
    /// <param name="timeout"> The shared daemon-list timeout budget. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-list execution result. </returns>
    ValueTask<DaemonListExecutionResult> GetListAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
