using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

namespace MackySoft.Ucli.Tests.Scenes;

public sealed class SceneTreeLiteDirtySourceProbeServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenModeIsOneshot_DoesNotReadSnapshot ()
    {
        var reader = new StubSceneTreeLiteSnapshotReader();
        var service = new SceneTreeLiteDirtySourceProbeService(reader);

        var result = await service.ProbeAsync(
            CreateProject(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            CancellationToken.None);

        Assert.False(result.HasDirtySource);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenDaemonReturnsDirtyLoadedScene_ReturnsDirtySource ()
    {
        var reader = new StubSceneTreeLiteSnapshotReader();
        var response = CreateResponse(new SceneTreeSourceState(SceneTreeSourceStateKind.LoadedScene, isDirty: true));
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Success(response));
        var service = new SceneTreeLiteDirtySourceProbeService(reader);

        var result = await service.ProbeAsync(
            CreateProject(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            CancellationToken.None);

        Assert.True(result.HasDirtySource);
        Assert.Same(response, result.Response);
        Assert.Equal(UnityExecutionMode.Daemon, reader.LastMode);
        Assert.True(reader.LastFailFast);
        Assert.True(reader.LastLoadedSceneOnly);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenDaemonReturnsCleanLoadedScene_ReturnsNotAvailable ()
    {
        var reader = new StubSceneTreeLiteSnapshotReader();
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Success(CreateResponse(
            new SceneTreeSourceState(SceneTreeSourceStateKind.LoadedScene, isDirty: false))));
        var service = new SceneTreeLiteDirtySourceProbeService(reader);

        var result = await service.ProbeAsync(
            CreateProject(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Daemon,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            CancellationToken.None);

        Assert.False(result.HasDirtySource);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenDaemonReadFails_ReturnsNotAvailable ()
    {
        var reader = new StubSceneTreeLiteSnapshotReader();
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Failure("daemon is not running", UcliCoreErrorCodes.InternalError));
        var service = new SceneTreeLiteDirtySourceProbeService(reader);

        var result = await service.ProbeAsync(
            CreateProject(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            CancellationToken.None);

        Assert.False(result.HasDirtySource);
        Assert.Equal(1, reader.CallCount);
    }

    private static ResolvedUnityProjectContext CreateProject ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static IpcIndexSceneTreeLiteReadResponse CreateResponse (SceneTreeSourceState sourceState)
    {
        return new IpcIndexSceneTreeLiteReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Main.unity",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract("Root", "GlobalObjectId_V1-1-1-1", Array.Empty<IndexSceneTreeLiteNodeJsonContract>(), IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ],
            SourceState: sourceState);
    }

    private sealed class StubSceneTreeLiteSnapshotReader : ISceneTreeLiteSnapshotReader
    {
        private readonly Queue<SceneTreeLiteSnapshotFetchResult> results = new();

        public int CallCount { get; private set; }

        public UnityExecutionMode LastMode { get; private set; }

        public bool LastFailFast { get; private set; }

        public bool LastLoadedSceneOnly { get; private set; }

        public void Enqueue (SceneTreeLiteSnapshotFetchResult result)
        {
            results.Enqueue(result);
        }

        public ValueTask<SceneTreeLiteSnapshotFetchResult> ReadAsync (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            string scenePath,
            bool failFast = false,
            bool loadedSceneOnly = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastMode = mode;
            LastFailFast = failFast;
            LastLoadedSceneOnly = loadedSceneOnly;
            if (!results.TryDequeue(out var result))
            {
                throw new InvalidOperationException("Scene snapshot result is not configured.");
            }

            return ValueTask.FromResult(result);
        }
    }
}
