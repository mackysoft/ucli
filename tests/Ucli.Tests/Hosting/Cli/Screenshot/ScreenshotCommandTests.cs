using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Screenshot;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Tests;

public sealed class ScreenshotCommandTests
{
    [Theory]
    [InlineData(UcliCommandNames.GameSubcommand)]
    [InlineData(UcliCommandNames.SceneSubcommand)]
    [Trait("Size", "Medium")]
    public async Task Help_DoesNotAdvertiseUnusedExecutionMode (string target)
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Screenshot,
            target,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.DoesNotContain(UcliContractConstants.CliOption.Mode, result.StdOut, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UcliCommandNames.GameSubcommand, UcliCommandNames.ScreenshotGame)]
    [InlineData(UcliCommandNames.SceneSubcommand, UcliCommandNames.ScreenshotScene)]
    [Trait("Size", "Medium")]
    public async Task RemovedModeOption_IsRejectedBeforeCapture (
        string target,
        string resultCommandName)
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Screenshot,
            target,
            UcliContractConstants.CliOption.Mode,
            "daemon");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, UcliContractConstants.CliOption.Mode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, resultCommandName);
    }

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

    [Fact]
    [Trait("Size", "Small")]
    public async Task Game_WhenCaptureSucceeds_EmitsMetadataAndArtifactReference ()
    {
        var service = new RecordingScreenshotCaptureService((_, _) => ValueTask.FromResult(
            ScreenshotCaptureResult.Success(CreateOutput(
                IpcScreenshotTarget.Game,
                requestedWidth: 1920,
                requestedHeight: 1080))));
        var command = new ScreenshotGameCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.GameAsync(
            projectPath: AbsolutePath.Parse(ProjectPathTestValues.RepositoryUnityProject),
            width: "1920",
            height: "1080",
            timeout: "5000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var input = Assert.Single(service.Inputs);
        Assert.Equal(IpcScreenshotTarget.Game, input.Target);
        Assert.Equal(new PixelDimensions(1920, 1080), input.RequestedDimensions);
        Assert.Equal(5000, input.TimeoutMilliseconds);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(outputJson.RootElement, UcliCommandNames.ScreenshotGame);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("capture", capture => capture
                .HasString("target", "game")
                .HasString("sizeMode", "requestedResolution")
                .HasProperty("requestedDimensions", dimensions => dimensions
                    .HasInt32("width", 1920)
                    .HasInt32("height", 1080))
                .HasProperty("dimensions", dimensions => dimensions
                    .HasInt32("width", 1920)
                    .HasInt32("height", 1080))
                .HasString("projectColorSpace", "linear")
                .HasString("lifecycleStateAtCapture", "ready")
                .HasString("compileStateAtCapture", "ready")
                .HasProperty("generations", generations => generations
                    .HasInt32("compileGeneration", 5)
                    .HasInt32("domainReloadGeneration", 7)
                    .HasInt32("assetRefreshGeneration", 11)
                    .HasInt32("playModeGeneration", 13))
                .HasString("playModeState", "stopped"))
            .HasProperty("artifact", artifact => artifact
                .HasString(
                    "locationKind",
                    TextVocabulary.GetText(ArtifactLocationKind.Path))
                .HasString(
                    "kind",
                    TextVocabulary.GetText(ScreenshotArtifactKind.Screenshot))
                .HasString(
                    "mediaType",
                    TextVocabulary.GetText(ScreenshotArtifactMediaType.Png))
                .HasString("path", ".ucli/local/projects/<projectStorageKey>/artifacts/screenshot/<captureStorageKey>/screenshot.png")
                .HasString("digest", new string('a', 64))
                .HasInt32("sizeBytes", 4096)
                .HasString("createdAtUtc", "2026-07-11T01:02:03.0000000Z"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Scene_WhenCaptureSucceeds_DispatchesCurrentSurfaceTarget ()
    {
        var service = new RecordingScreenshotCaptureService((_, _) => ValueTask.FromResult(
            ScreenshotCaptureResult.Success(CreateOutput(
                IpcScreenshotTarget.Scene,
                requestedWidth: null,
                requestedHeight: null))));
        var command = new ScreenshotSceneCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.SceneAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var input = Assert.Single(service.Inputs);
        Assert.Equal(IpcScreenshotTarget.Scene, input.Target);
        Assert.Null(input.RequestedDimensions);
    }

    private static RecordingScreenshotCaptureService CreateFailIfCalledService ()
    {
        return new RecordingScreenshotCaptureService((_, _) =>
            throw new InvalidOperationException("Capture service should not be called."));
    }

    private static ScreenshotCaptureOutput CreateOutput (
        IpcScreenshotTarget target,
        int? requestedWidth,
        int? requestedHeight)
    {
        return new ScreenshotCaptureOutput(
            ProjectIdentityInfoTestFactory.CreateWithProjectPath(
                projectPath: ProjectPathTestValues.RepositoryUnityProject,
                projectFingerprint: ProjectFingerprintTestFactory.Create("screenshot-command"),
                unityVersion: "6000.0.77f1"),
            new IpcScreenshotCapture(
                target,
                requestedWidth.HasValue
                    ? IpcScreenshotSizeMode.RequestedResolution
                    : IpcScreenshotSizeMode.CurrentSurface,
                requestedWidth.HasValue
                    ? new PixelDimensions(requestedWidth.Value, requestedHeight!.Value)
                    : null,
                new PixelDimensions(requestedWidth ?? 1280, requestedHeight ?? 720),
                UnityProjectColorSpace.Linear,
                new UnityEditorStateSnapshot(
                    UnityEditorMode.Gui,
                    UnityEditorLifecycleState.Ready,
                    UnityEditorCompileState.Ready,
                    new UnityEditorGenerationSnapshot(5, 7, 11, 13),
                    new UnityEditorPlayModeSnapshot(
                        UnityEditorPlayModeState.Stopped,
                        UnityEditorPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false))),
            new PathArtifactRef(
                new ArtifactKind(TextVocabulary.GetText(ScreenshotArtifactKind.Screenshot)),
                new ArtifactMediaType(TextVocabulary.GetText(ScreenshotArtifactMediaType.Png)),
                new ArtifactPath(".ucli/local/projects/<projectStorageKey>/artifacts/screenshot/<captureStorageKey>/screenshot.png"),
                Sha256Digest.Parse(new string('a', 64)),
                sizeBytes: 4096,
                new DateTimeOffset(2026, 7, 11, 1, 2, 3, TimeSpan.Zero)));
    }
}
