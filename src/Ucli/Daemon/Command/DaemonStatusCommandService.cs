using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Implements daemon-status command workflow orchestration. </summary>
internal sealed class DaemonStatusCommandService : IDaemonStatusCommandService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IDaemonStatusOperation daemonStatusOperation;

    private readonly IDaemonSessionOutputMapper daemonSessionOutputMapper;

    /// <summary> Initializes a new instance of the <see cref="DaemonStatusCommandService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command execution-context resolver dependency. </param>
    /// <param name="daemonStatusOperation"> The daemon status-operation dependency. </param>
    /// <param name="daemonSessionOutputMapper"> The daemon session-output mapper dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStatusCommandService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IDaemonStatusOperation daemonStatusOperation,
        IDaemonSessionOutputMapper daemonSessionOutputMapper)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonStatusOperation = daemonStatusOperation ?? throw new ArgumentNullException(nameof(daemonStatusOperation));
        this.daemonSessionOutputMapper = daemonSessionOutputMapper ?? throw new ArgumentNullException(nameof(daemonSessionOutputMapper));
    }

    /// <summary> Executes one daemon-status workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-status execution result. </returns>
    public async ValueTask<DaemonStatusExecutionResult> GetStatus (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.Resolve(projectPath, timeout, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return DaemonStatusExecutionResult.Failure(contextResult.Error!);
        }

        var executionContext = contextResult.Context!;
        var statusResult = await daemonStatusOperation.GetStatus(
                executionContext.Context.UnityProject,
                executionContext.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!statusResult.IsSuccess)
        {
            return DaemonStatusExecutionResult.Failure(statusResult.Error ?? ExecutionError.InternalError(
                "Daemon status operation failed without structured error details."));
        }

        if (!DaemonStatusStateCodec.TryToValue(statusResult.Status, out var daemonStatus))
        {
            return DaemonStatusExecutionResult.Failure(ExecutionError.InternalError(
                $"Daemon status returned unsupported status: {statusResult.Status}."));
        }

        var output = new DaemonStatusExecutionOutput(
            DaemonStatus: daemonStatus!,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds),
            Session: statusResult.Session is null
                ? null
                : daemonSessionOutputMapper.ToOutput(statusResult.Session));
        return DaemonStatusExecutionResult.Success(output);
    }
}