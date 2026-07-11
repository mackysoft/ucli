using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Hosting.Cli.Screenshot;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class ScreenshotCommandTests
{
    [Theory]
    [InlineData("1920", null)]
    [InlineData(null, "1080")]
    [InlineData("0", "1080")]
    [InlineData("1920", "-1")]
    [Trait("Size", "Small")]
    public async Task Game_WithInvalidRequestedSize_RejectsBeforeCapture (
        string? width,
        string? height)
    {
        var service = CreateFailIfCalledService();
        var command = new ScreenshotGameCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.GameAsync(
            width: width,
            height: height,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Empty(service.Inputs);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasInvalidArgumentError(
            outputJson.RootElement,
            UcliCommandNames.ScreenshotGame);
    }

    [Theory]
    [InlineData("oneshot")]
    [InlineData("invalid")]
    [Trait("Size", "Small")]
    public async Task Scene_WithUnsupportedMode_RejectsBeforeCapture (string mode)
    {
        var service = CreateFailIfCalledService();
        var command = new ScreenshotSceneCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.SceneAsync(
            mode: mode,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Empty(service.Inputs);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Game_WhenCaptureSucceeds_EmitsMetadataAndArtifactReference ()
    {
        var service = new RecordingScreenshotCaptureService((_, _) => ValueTask.FromResult(
            ScreenshotCaptureResult.Success(CreateOutput(
                ScreenshotCaptureTarget.Game,
                requestedWidth: 1920,
                requestedHeight: 1080))));
        var command = new ScreenshotGameCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.GameAsync(
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            width: "1920",
            height: "1080",
            timeout: "5000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var input = Assert.Single(service.Inputs);
        Assert.Equal(ScreenshotCaptureTarget.Game, input.Target);
        Assert.Equal(1920, input.RequestedWidth);
        Assert.Equal(1080, input.RequestedHeight);
        Assert.Equal(5000, input.TimeoutMilliseconds);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(outputJson.RootElement, UcliCommandNames.ScreenshotGame);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("capture", capture => capture
                .HasString("target", "game")
                .HasString("sizeMode", "requestedResolution")
                .HasInt32("requestedWidth", 1920)
                .HasInt32("requestedHeight", 1080)
                .HasInt32("width", 1920)
                .HasInt32("height", 1080)
                .HasString("colorSpace", "linear")
                .HasString("lifecycleStateAtCapture", "ready")
                .HasString("compileStateAtCapture", "ready")
                .HasInt32("domainReloadGeneration", 7)
                .HasString("playModeState", "playing"))
            .HasProperty("artifact", artifact => artifact
                .HasString("kind", "screenshot")
                .HasString("mediaType", "image/png")
                .HasString("path", ".ucli/local/fingerprints/pf_test/artifacts/screenshot/capture/screenshot.png")
                .HasString("digest", new string('a', 64))
                .HasInt32("sizeBytes", 4096)
                .HasString("createdAtUtc", "2026-07-11T01:02:03+00:00"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Scene_WhenCaptureSucceeds_DispatchesCurrentSurfaceTarget ()
    {
        var service = new RecordingScreenshotCaptureService((_, _) => ValueTask.FromResult(
            ScreenshotCaptureResult.Success(CreateOutput(
                ScreenshotCaptureTarget.Scene,
                requestedWidth: null,
                requestedHeight: null))));
        var command = new ScreenshotSceneCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.SceneAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var input = Assert.Single(service.Inputs);
        Assert.Equal(ScreenshotCaptureTarget.Scene, input.Target);
        Assert.Null(input.RequestedWidth);
        Assert.Null(input.RequestedHeight);
    }

    private static RecordingScreenshotCaptureService CreateFailIfCalledService ()
    {
        return new RecordingScreenshotCaptureService((_, _) =>
            throw new InvalidOperationException("Capture service should not be called."));
    }

    private static ScreenshotCaptureOutput CreateOutput (
        ScreenshotCaptureTarget target,
        int? requestedWidth,
        int? requestedHeight)
    {
        return new ScreenshotCaptureOutput(
            new ProjectIdentityInfo("/repo/UnityProject", "pf_test", "6000.0.77f1"),
            target,
            requestedWidth,
            requestedHeight,
            Width: requestedWidth ?? 1280,
            Height: requestedHeight ?? 720,
            ColorSpace: "linear",
            LifecycleStateAtCapture: "ready",
            CompileStateAtCapture: "ready",
            DomainReloadGeneration: 7,
            PlayModeState: "playing",
            ArtifactPath: ".ucli/local/fingerprints/pf_test/artifacts/screenshot/capture/screenshot.png",
            ArtifactDigest: new string('a', 64),
            ArtifactSizeBytes: 4096,
            ArtifactCreatedAtUtc: new DateTimeOffset(2026, 7, 11, 1, 2, 3, TimeSpan.Zero));
    }
}
