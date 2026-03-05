using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Implements daemon-start command workflow orchestration. </summary>
internal sealed class DaemonStartCommandService : IDaemonStartCommandService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IDaemonStartOperation daemonStartOperation;

    private readonly IDaemonSessionOutputMapper daemonSessionOutputMapper;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartCommandService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command execution-context resolver dependency. </param>
    /// <param name="daemonStartOperation"> The daemon start-operation dependency. </param>
    /// <param name="daemonSessionOutputMapper"> The daemon session-output mapper dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartCommandService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IDaemonStartOperation daemonStartOperation,
        IDaemonSessionOutputMapper daemonSessionOutputMapper)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonStartOperation = daemonStartOperation ?? throw new ArgumentNullException(nameof(daemonStartOperation));
        this.daemonSessionOutputMapper = daemonSessionOutputMapper ?? throw new ArgumentNullException(nameof(daemonSessionOutputMapper));
    }

    /// <summary> Executes one daemon-start workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-start execution result. </returns>
    public async ValueTask<DaemonStartExecutionResult> Start (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.Resolve(
                UcliCommandIds.DaemonStart,
                projectPath,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return DaemonStartExecutionResult.Failure(contextResult.Error!);
        }

        var executionContext = contextResult.Context!;
        var startResult = await daemonStartOperation.Start(
                executionContext.Context.UnityProject,
                executionContext.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!startResult.IsSuccess)
        {
            return DaemonStartExecutionResult.Failure(startResult.Error ?? ExecutionError.InternalError(
                "Daemon start operation failed without structured error details."));
        }

        if (!DaemonStartStateCodec.TryToValue(startResult.Status, out var startStatus))
        {
            return DaemonStartExecutionResult.Failure(ExecutionError.InternalError(
                $"Daemon start returned unsupported status: {startResult.Status}."));
        }

        var output = new DaemonStartExecutionOutput(
            StartStatus: startStatus!,
            DaemonStatus: DaemonStatusStateCodec.Running,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds),
            Session: daemonSessionOutputMapper.ToOutput(startResult.Session!));
        return DaemonStartExecutionResult.Success(output);
    }
}