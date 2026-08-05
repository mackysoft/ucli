using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Hosting.Cli.Screenshot;

/// <summary> Creates public command results for screenshot captures. </summary>
internal static class ScreenshotCommandResultFactory
{
    /// <summary> Gets the serializer contract used by successful screenshot command payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(ScreenshotCommandPayload));

    /// <summary> Gets the serializer contract used by failed screenshot command payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(EmptyCommandPayload));

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
            new ScreenshotCommandPayload(
                output.Project,
                new ScreenshotCaptureCommandPayload(
                    capture.Target,
                    capture.SizeMode,
                    capture.RequestedDimensions,
                    capture.Dimensions,
                    capture.ProjectColorSpace,
                    capture.State.LifecycleState,
                    capture.State.CompileState,
                    capture.State.Generations,
                    capture.State.PlayMode.State),
                artifact));
    }

    private sealed record ScreenshotCommandPayload (
        ProjectIdentityInfo Project,
        ScreenshotCaptureCommandPayload Capture,
        ArtifactRef Artifact);

    private sealed record ScreenshotCaptureCommandPayload (
        IpcScreenshotTarget Target,
        IpcScreenshotSizeMode SizeMode,
        PixelDimensions? RequestedDimensions,
        PixelDimensions Dimensions,
        UnityProjectColorSpace ProjectColorSpace,
        UnityEditorLifecycleState LifecycleStateAtCapture,
        UnityEditorCompileState CompileStateAtCapture,
        UnityEditorGenerationSnapshot Generations,
        UnityEditorPlayModeState PlayModeState);

}
