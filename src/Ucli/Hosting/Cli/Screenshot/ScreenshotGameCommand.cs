using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Screenshot;

/// <summary> Provides the <c>screenshot game</c> CLI entry point. </summary>
internal sealed class ScreenshotGameCommand
{
    private readonly IScreenshotCaptureService captureService;
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new screenshot GameView command. </summary>
    public ScreenshotGameCommand (
        IScreenshotCaptureService captureService,
        ICommandResultWriter commandResultWriter)
    {
        this.captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Captures the main GameView presentation surface. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode. Accepts auto or daemon.</param>
    /// <param name="width">Requested GameView capture width. Must be specified with height.</param>
    /// <param name="height">Requested GameView capture height. Must be specified with width.</param>
    /// <param name="timeout">Capture timeout in milliseconds.</param>
    /// <param name="cancellationToken">The cancellation token propagated by command execution.</param>
    /// <returns>The exit code contained in the emitted command result.</returns>
    [Command(UcliCommandNames.GameSubcommand)]
    public async Task<int> GameAsync (
        string? projectPath = null,
        string? mode = null,
        string? width = null,
        string? height = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var options = ScreenshotCommandOptionsNormalizer.NormalizeGame(mode, width, height, timeout);
        if (!options.IsSuccess)
        {
            var invalidResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.ScreenshotGame,
                options.Error!);
            commandResultWriter.WriteToStandardOutput(invalidResult);
            return invalidResult.ExitCode;
        }

        var result = await captureService.CaptureAsync(
                new ScreenshotCaptureInput(
                    ScreenshotCaptureTarget.Game,
                    projectPath,
                    options.RequestedWidth,
                    options.RequestedHeight,
                    options.TimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = ScreenshotCommandResultFactory.Create(UcliCommandNames.ScreenshotGame, result);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
