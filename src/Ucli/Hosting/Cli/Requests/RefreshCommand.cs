using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the refresh CLI command entry point. </summary>
internal sealed class RefreshCommand
{
    private readonly IRefreshService refreshService;

    private readonly ICommandResultWriter commandResultWriter;

    private readonly ILifecycleExecutionCliInvocationFactory invocationFactory;

    /// <summary> Initializes a new instance of the RefreshCommand class. </summary>
    /// <param name="refreshService"> The refresh service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when refreshService is null. </exception>
    public RefreshCommand (
        IRefreshService refreshService,
        ICommandResultWriter commandResultWriter,
        ILifecycleExecutionCliInvocationFactory invocationFactory)
    {
        this.refreshService = refreshService ?? throw new ArgumentNullException(nameof(refreshService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
        this.invocationFactory = invocationFactory ?? throw new ArgumentNullException(nameof(invocationFactory));
    }

    /// <summary> Executes the refresh command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet ready.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Refresh)]
    public async Task<int> RefreshAsync (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();
        var requestId = Guid.NewGuid();

        var normalizedModeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!normalizedModeResult.IsSuccess)
        {
            var errorResult = RefreshCommandResultFactory.CreateExecutionError(requestId, normalizedModeResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            var errorResult = RefreshCommandResultFactory.CreateExecutionError(requestId, normalizedTimeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var invocationResult = await invocationFactory.CreateRefreshStartAsync(
                projectPath,
                normalizedModeResult.Mode ?? UnityExecutionMode.Auto,
                normalizedTimeoutResult.TimeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        var executionResult = invocationResult.IsSuccess
            ? await refreshService.StartAsync(
                    requestId,
                    invocationResult.Invocation!,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false)
            : RefreshExecutionResult.Failure(
                invocationResult.Failure!,
                invocationResult.Project is null
                    ? null
                    : new RefreshExecutionErrorOutput(
                        invocationResult.Project,
                        requestId,
                        LifecycleExecutionRef: null,
                        ExecutionApplicationState.NotApplied,
                        Refresh: null,
                        ObservedLifecycle: null,
                        ReadPostcondition: null));
        var commandResult = RefreshCommandResultFactory.Create(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
