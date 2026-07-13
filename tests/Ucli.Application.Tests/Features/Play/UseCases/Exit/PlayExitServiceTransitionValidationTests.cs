using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayExitServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayExitServiceTransitionValidationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAlreadyExitedChangesGeneration_ReturnsStateUnknown ()
    {
        var before = CreateSnapshot(
            "compiling",
            "compile",
            false,
            CreateStoppedPlayMode("9"));
        var after = CreateSnapshot(
            "compiling",
            "compile",
            false,
            CreateStoppedPlayMode("10"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.AlreadyExited,
            before)
        {
            After = after,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenResponseProjectFingerprintDiffers_ReturnsMismatchFailure ()
    {
        var before = CreateSnapshot(
            "playmode",
            "playMode",
            false,
            CreatePlayingPlayMode("2"),
            projectFingerprint: "other-project-fingerprint");
        var after = CreateSnapshot(
            "ready",
            null,
            true,
            CreateStoppedPlayMode("3"),
            projectFingerprint: "other-project-fingerprint");
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Exited,
            before)
        {
            After = after,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("projectFingerprint mismatch", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExitedDoesNotChangeGeneration_ReturnsStateUnknown ()
    {
        var before = CreateSnapshot("playmode", "playMode", false, CreatePlayingPlayMode("2"));
        var after = CreateSnapshot("ready", null, true, CreateStoppedPlayMode("2"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Exited,
            before)
        {
            After = after,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExitedAfterSnapshotIsStillPlaymode_ReturnsStateUnknown ()
    {
        var before = CreateSnapshot("playmode", "playMode", false, CreatePlayingPlayMode("2"));
        var after = CreateSnapshot(
            "playmode",
            "playMode",
            false,
            CreateStoppedPlayMode("3"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Exited,
            before)
        {
            After = after,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSuccessTransitionContainsErrorFields_ReturnsStateUnknown ()
    {
        var before = CreateSnapshot("playmode", "playMode", false, CreatePlayingPlayMode("2"));
        var after = CreateSnapshot("ready", null, true, CreateStoppedPlayMode("3"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Exited,
            before)
        {
            After = after,
            Observed = before,
            ApplicationState = IpcPlayApplicationStateNames.NotApplied,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTimeoutTransitionContainsAfter_ReturnsStateUnknown ()
    {
        var before = CreateSnapshot("playmode", "playMode", false, CreatePlayingPlayMode("2"));
        var observed = CreateSnapshot("playmode", "playMode", false, new IpcPlayModeSnapshot(
            State: "exiting",
            Transition: "exiting",
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true,
            Generation: "2"));
        var after = CreateSnapshot("ready", null, true, CreateStoppedPlayMode("3"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Timeout,
            before)
        {
            After = after,
            Observed = observed,
            ApplicationState = IpcPlayApplicationStateNames.Indeterminate,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponse(
            response,
            PlayModeErrorCodes.PlayModeTransitionTimeout,
            "Unity Play Mode exit timed out.")));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTimeoutApplicationStateIsNotIndeterminate_ReturnsStateUnknown ()
    {
        var before = CreateSnapshot("playmode", "playMode", false, CreatePlayingPlayMode("2"));
        var observed = CreateSnapshot("playmode", "playMode", false, new IpcPlayModeSnapshot(
            State: "exiting",
            Transition: "exiting",
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true,
            Generation: "2"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Exit,
            IpcPlayTransitionResultNames.Timeout,
            before)
        {
            Observed = observed,
            ApplicationState = IpcPlayApplicationStateNames.NotApplied,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponse(
            response,
            PlayModeErrorCodes.PlayModeTransitionTimeout,
            "Unity Play Mode exit timed out.")));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeStateUnknown, result.Error!.Code);
    }
}
