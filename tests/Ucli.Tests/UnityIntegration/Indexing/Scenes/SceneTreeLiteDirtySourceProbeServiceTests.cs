using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Indexing.Scenes;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

namespace MackySoft.Ucli.Tests.Scenes;

public sealed class SceneTreeLiteDirtySourceProbeServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenModeIsOneshot_DoesNotReadSnapshot ()
    {
        var service = new SceneTreeLiteDirtySourceProbeService(new UnexpectedSceneTreeLiteSnapshotReader(
            "Oneshot dirty source probe must stop before daemon snapshot read."));

        var result = await service.ProbeAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromSeconds(1),
            new UnityScenePath("Assets/Scenes/Main.unity"),
            CancellationToken.None);

        Assert.False(result.HasDirtySource);
        Assert.Equal("oneshot execution cannot observe an existing Unity daemon scene.", result.FallbackReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenDaemonReturnsDirtyLoadedScene_ReturnsDirtySource ()
    {
        var reader = new RecordingSceneTreeLiteSnapshotReader();
        var snapshot = SceneTreeLiteSourceSnapshotTestFactory.Create(
            "Assets/Scenes/Main.unity",
            "Root",
            new SceneTreeSourceState(SceneTreeSourceStateKind.LoadedScene, isDirty: true));
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Success(snapshot));
        var service = new SceneTreeLiteDirtySourceProbeService(reader);

        var result = await service.ProbeAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            new UnityScenePath("Assets/Scenes/Main.unity"),
            CancellationToken.None);

        Assert.True(result.HasDirtySource);
        Assert.Same(snapshot, result.Snapshot);
        Assert.Equal("Dirty loaded scene is open in Unity daemon.", result.FallbackReason);
        SceneTreeLiteSnapshotReaderAssert.LoadedSceneProbeRequested(
            reader,
            "Assets/Scenes/Main.unity");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenDaemonReturnsCleanLoadedScene_ReturnsNotAvailable ()
    {
        var reader = new RecordingSceneTreeLiteSnapshotReader();
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Success(SceneTreeLiteSourceSnapshotTestFactory.Create(
            "Assets/Scenes/Main.unity",
            "Root",
            new SceneTreeSourceState(SceneTreeSourceStateKind.LoadedScene, isDirty: false))));
        var service = new SceneTreeLiteDirtySourceProbeService(reader);

        var result = await service.ProbeAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Daemon,
            TimeSpan.FromSeconds(1),
            new UnityScenePath("Assets/Scenes/Main.unity"),
            CancellationToken.None);

        Assert.False(result.HasDirtySource);
        Assert.Equal("Unity daemon scene is not dirty loaded source.", result.FallbackReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenDaemonReadFails_ReturnsNotAvailable ()
    {
        var reader = new RecordingSceneTreeLiteSnapshotReader();
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Failure("daemon is not running", UcliCoreErrorCodes.InternalError));
        var service = new SceneTreeLiteDirtySourceProbeService(reader);

        var result = await service.ProbeAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            new UnityScenePath("Assets/Scenes/Main.unity"),
            CancellationToken.None);

        Assert.False(result.HasDirtySource);
        Assert.Equal("daemon is not running", result.FallbackReason);
    }

}
