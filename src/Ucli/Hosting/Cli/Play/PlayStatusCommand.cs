using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Provides the <c>play status</c> CLI command entry point. </summary>
internal sealed class PlayStatusCommand
{
    private readonly IPlayStatusService playStatusService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="PlayStatusCommand" /> class. </summary>
    /// <param name="playStatusService"> The Play Mode status service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public PlayStatusCommand (
        IPlayStatusService playStatusService,
        ICommandResultWriter commandResultWriter)
    {
        this.playStatusService = playStatusService ?? throw new ArgumentNullException(nameof(playStatusService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>play status</c> and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Status)]
    public async Task<int> StatusAsync (
        string? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();

        var timeoutNormalizationResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutNormalizationResult.IsSuccess)
        {
            var invalidTimeoutResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.PlayStatus,
                timeoutNormalizationResult.Error!);
            commandResultWriter.WriteToStandardOutput(invalidTimeoutResult);
            return invalidTimeoutResult.ExitCode;
        }

        var input = new PlayStatusCommandInput(
            ProjectPath: projectPath,
            TimeoutMilliseconds: timeoutNormalizationResult.TimeoutMilliseconds);
        var executionResult = await playStatusService.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
        var commandResult = PlayStatusCommandResultFactory.Create(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
