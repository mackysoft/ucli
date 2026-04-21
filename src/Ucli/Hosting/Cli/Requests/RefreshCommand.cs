using ConsoleAppFramework;
using MackySoft.Ucli.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the <c>refresh</c> CLI command entry point. </summary>
internal sealed class RefreshCommand
{
    private readonly IRefreshService refreshService;

    /// <summary> Initializes a new instance of the <see cref="RefreshCommand" /> class. </summary>
    /// <param name="refreshService"> The refresh service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="refreshService" /> is <see langword="null" />. </exception>
    public RefreshCommand (IRefreshService refreshService)
    {
        this.refreshService = refreshService ?? throw new ArgumentNullException(nameof(refreshService));
    }

    /// <summary> Executes the <c>refresh</c> command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (<c>auto|daemon|oneshot</c>).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet <c>ready</c>.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Refresh)]
    public async Task<int> Refresh (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();

        var normalizedModeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!normalizedModeResult.IsSuccess)
        {
            var errorResult = RefreshCommandResultFactory.CreateExecutionError(normalizedModeResult.Error!);
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            var errorResult = RefreshCommandResultFactory.CreateExecutionError(normalizedTimeoutResult.Error!);
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var executionResult = await refreshService.Execute(
                new RefreshCommandInput(
                    ProjectPath: projectPath,
                    Mode: normalizedModeResult.Mode,
                    TimeoutMilliseconds: normalizedTimeoutResult.TimeoutMilliseconds,
                    FailFast: failFast),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = RefreshCommandResultFactory.Create(executionResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}