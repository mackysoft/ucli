using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Screenshot;

/// <summary> Creates public command results for screenshot captures. </summary>
internal static class ScreenshotCommandResultFactory
{
    /// <summary> Creates one screenshot command result. </summary>
    public static CommandResult Create (string command, ScreenshotCaptureResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
        {
            return CommandResultFactory.FromExecutionError(command, result.Error!);
        }

        var output = result.Output;
        var capture = output.Capture;
        var artifact = output.Artifact;
        return CommandResult.Success(
            command,
            "Screenshot capture completed.",
            new
            {
                project = ProjectIdentityPayloadProjector.Create(output.Project),
                capture = new
                {
                    capture.Target,
                    capture.SizeMode,
                    capture.RequestedWidth,
                    capture.RequestedHeight,
                    capture.Width,
                    capture.Height,
                    capture.ColorSpace,
                    lifecycleStateAtCapture = capture.State.LifecycleState,
                    compileStateAtCapture = capture.State.CompileState,
                    generations = capture.State.Generations,
                    playModeState = capture.State.PlayMode.State,
                },
                artifact = new
                {
                    kind = ScreenshotArtifactKind.Screenshot,
                    mediaType = ScreenshotArtifactContract.MediaType,
                    artifact.Path,
                    artifact.Digest,
                    artifact.SizeBytes,
                    artifact.CreatedAtUtc,
                },
            });
    }
}
