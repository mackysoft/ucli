using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Provides the <c>play exit</c> CLI command entry point. </summary>
internal sealed class PlayExitCommand
{
    private readonly IPlayExitService playExitService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="PlayExitCommand" /> class. </summary>
    /// <param name="playExitService"> The Play Mode exit service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public PlayExitCommand (
        IPlayExitService playExitService,
        ICommandResultWriter commandResultWriter)
    {
        this.playExitService = playExitService ?? throw new ArgumentNullException(nameof(playExitService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>play exit</c> and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ExitSubcommand)]
    public async Task<int> ExitAsync (
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
                UcliCommandNames.PlayExit,
                timeoutNormalizationResult.Error!);
            commandResultWriter.WriteToStandardOutput(invalidTimeoutResult);
            return invalidTimeoutResult.ExitCode;
        }

        var input = new PlayExitCommandInput(
            ProjectPath: projectPath,
            TimeoutMilliseconds: timeoutNormalizationResult.TimeoutMilliseconds);
        var executionResult = await playExitService.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
        var commandResult = PlayExitCommandResultFactory.Create(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
