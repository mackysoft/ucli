using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;

/// <summary> Implements daemon-cleanup command workflow orchestration. </summary>
internal sealed class DaemonCleanupService : IDaemonCleanupService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IDaemonCleanupOperation daemonCleanupOperation;

    /// <summary> Initializes a new instance of the <see cref="DaemonCleanupService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command execution-context resolver dependency. </param>
    /// <param name="daemonCleanupOperation"> The daemon cleanup-operation dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonCleanupService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IDaemonCleanupOperation daemonCleanupOperation)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonCleanupOperation = daemonCleanupOperation ?? throw new ArgumentNullException(nameof(daemonCleanupOperation));
    }

    /// <summary> Executes one daemon-cleanup workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeoutMilliseconds"> The optional normalized timeout value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-cleanup execution result. </returns>
    public async ValueTask<DaemonCleanupExecutionResult> Cleanup (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.Resolve(
                UcliCommandIds.DaemonCleanup,
                projectPath,
                timeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return DaemonCleanupExecutionResult.Failure(contextResult.Error!);
        }

        var executionContext = contextResult.Context!;
        var cleanupResult = await daemonCleanupOperation.Cleanup(
                executionContext.Context.UnityProject,
                executionContext.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!cleanupResult.IsSuccess)
        {
            return DaemonCleanupExecutionResult.Failure(cleanupResult.Error ?? ExecutionError.InternalError(
                "Daemon cleanup operation failed without structured error details."));
        }

        var output = new DaemonCleanupExecutionOutput(
            CleanupStatus: cleanupResult.Status,
            SkipReason: cleanupResult.SkipReason,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds));
        return DaemonCleanupExecutionResult.Success(output);
    }
}
