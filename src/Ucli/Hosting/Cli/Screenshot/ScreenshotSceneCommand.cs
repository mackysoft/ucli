using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Screenshot;

/// <summary> Provides the <c>screenshot scene</c> CLI entry point. </summary>
internal sealed class ScreenshotSceneCommand
{
    private readonly IScreenshotCaptureService captureService;
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new screenshot SceneView command. </summary>
    public ScreenshotSceneCommand (
        IScreenshotCaptureService captureService,
        ICommandResultWriter commandResultWriter)
    {
        this.captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Captures the active SceneView presentation surface. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode. Accepts auto or daemon.</param>
    /// <param name="timeout">Capture timeout in milliseconds.</param>
    /// <param name="cancellationToken">The cancellation token propagated by command execution.</param>
    /// <returns>The exit code contained in the emitted command result.</returns>
    [Command(UcliCommandNames.SceneSubcommand)]
    public async Task<int> SceneAsync (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var options = ScreenshotCommandOptionsNormalizer.NormalizeScene(mode, timeout);
        if (!options.IsSuccess)
        {
            var invalidResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.ScreenshotScene,
                options.Error!);
            commandResultWriter.WriteToStandardOutput(invalidResult);
            return invalidResult.ExitCode;
        }

        var result = await captureService.CaptureAsync(
                new ScreenshotCaptureInput(
                    ScreenshotCaptureTarget.Scene,
                    projectPath,
                    RequestedWidth: null,
                    RequestedHeight: null,
                    options.TimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = ScreenshotCommandResultFactory.Create(UcliCommandNames.ScreenshotScene, result);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
