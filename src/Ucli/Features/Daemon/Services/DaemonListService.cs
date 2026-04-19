using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Implements daemon-list command workflow orchestration. </summary>
internal sealed class DaemonListService : IDaemonListService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IDaemonListQueryService daemonListQueryService;

    /// <summary> Initializes a new instance of the <see cref="DaemonListService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command execution-context resolver dependency. </param>
    /// <param name="daemonListQueryService"> The daemon-list query-service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonListService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IDaemonListQueryService daemonListQueryService)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonListQueryService = daemonListQueryService ?? throw new ArgumentNullException(nameof(daemonListQueryService));
    }

    /// <summary> Executes one daemon-list workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-list execution result. </returns>
    public async ValueTask<DaemonListExecutionResult> GetList (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.Resolve(
                UcliCommandIds.DaemonList,
                projectPath,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return DaemonListExecutionResult.Failure(contextResult.Error!);
        }

        var executionContext = contextResult.Context!;
        return await daemonListQueryService.GetList(
                executionContext.Context.UnityProject,
                executionContext.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
    }
}