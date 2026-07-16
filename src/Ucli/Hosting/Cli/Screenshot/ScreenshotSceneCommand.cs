using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

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
    /// <param name="timeout">Capture timeout in milliseconds.</param>
    /// <param name="cancellationToken">The cancellation token propagated by command execution.</param>
    /// <returns>The exit code contained in the emitted command result.</returns>
    [Command(UcliCommandNames.SceneSubcommand)]
    public async Task<int> SceneAsync (
        string? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutResult.IsSuccess)
        {
            var invalidResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.ScreenshotScene,
                timeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(invalidResult);
            return invalidResult.ExitCode;
        }

        var result = await captureService.CaptureAsync(
                new ScreenshotCaptureInput(
                    IpcScreenshotTarget.Scene,
                    projectPath,
                    RequestedWidth: null,
                    RequestedHeight: null,
                    timeoutResult.TimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = ScreenshotCommandResultFactory.Create(UcliCommandNames.ScreenshotScene, result);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
