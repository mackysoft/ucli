using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;

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
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-cleanup execution result. </returns>
    public async ValueTask<DaemonCleanupExecutionResult> Cleanup (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.Resolve(
                UcliCommandIds.DaemonCleanup,
                projectPath,
                timeout,
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

        if (!DaemonCleanupStateCodec.TryToValue(cleanupResult.Status, out var cleanupStatus))
        {
            return DaemonCleanupExecutionResult.Failure(ExecutionError.InternalError(
                $"Daemon cleanup returned unsupported status: {cleanupResult.Status}."));
        }

        if (!DaemonCleanupSkipReasonCodec.TryToValue(cleanupResult.SkipReason, out var skipReason))
        {
            return DaemonCleanupExecutionResult.Failure(ExecutionError.InternalError(
                $"Daemon cleanup returned unsupported skip reason: {cleanupResult.SkipReason}."));
        }

        var output = new DaemonCleanupExecutionOutput(
            CleanupStatus: cleanupStatus!,
            SkipReason: skipReason,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds));
        return DaemonCleanupExecutionResult.Success(output);
    }
}