using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Contracts.Text;
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

        var output = result.Output!;
        return CommandResult.Success(
            command,
            "Screenshot capture completed.",
            new
            {
                project = ProjectIdentityPayloadProjector.Create(output.Project),
                capture = new
                {
                    target = ContractLiteralCodec.ToValue(output.Target),
                    sizeMode = output.RequestedWidth.HasValue ? "requestedResolution" : "currentSurface",
                    requestedWidth = output.RequestedWidth,
                    requestedHeight = output.RequestedHeight,
                    output.Width,
                    output.Height,
                    output.ColorSpace,
                    output.LifecycleStateAtCapture,
                    output.CompileStateAtCapture,
                    output.DomainReloadGeneration,
                    output.PlayModeState,
                },
                artifact = new
                {
                    kind = "screenshot",
                    mediaType = "image/png",
                    path = output.ArtifactPath,
                    digest = output.ArtifactDigest,
                    sizeBytes = output.ArtifactSizeBytes,
                    createdAtUtc = output.ArtifactCreatedAtUtc,
                },
            });
    }
}
