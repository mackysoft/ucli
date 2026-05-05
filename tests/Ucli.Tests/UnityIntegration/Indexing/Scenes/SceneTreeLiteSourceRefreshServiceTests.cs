using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Index;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

namespace MackySoft.Ucli.Tests.Scenes;

public sealed class SceneTreeLiteSourceRefreshServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_PersistsLookup_WhenSnapshotIsStable ()
    {
        var reader = new StubSceneTreeLiteSnapshotReader();
        var response = CreateResponse("Assets/Scenes/Main.unity", "Root");
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Success(response));
        var store = new StubSceneTreeLiteStore();
        var calculator = new StubSceneTreeLiteSourceHashCalculator();
        calculator.Enqueue("hash-1");
        calculator.Enqueue("hash-1");
        var service = new SceneTreeLiteSourceRefreshService(reader, store, calculator);

        var result = await service.Refresh(
            CreateProject(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            "Assets/Scenes/Main.unity",
            "readIndex stale.",
            failFast: true,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(response, result.Response);
        Assert.Equal("readIndex stale.", result.FallbackReason);
        Assert.Equal(1, reader.CallCount);
        Assert.Equal(UnityExecutionMode.Auto, reader.LastMode);
        Assert.True(reader.LastFailFast);
        Assert.Equal(2, calculator.CallCount);
        Assert.Equal(1, store.CallCount);
        Assert.Equal("hash-1", store.SourceInputsHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_ReturnsFirstSnapshot_WhenRetryReadFails ()
    {
        var reader = new StubSceneTreeLiteSnapshotReader();
        var firstResponse = CreateResponse("Assets/Scenes/Main.unity", "First");
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Success(firstResponse));
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Failure("retry read timed out", IpcErrorCodes.InternalError));
        var store = new StubSceneTreeLiteStore();
        var calculator = new StubSceneTreeLiteSourceHashCalculator();
        calculator.Enqueue("hash-1");
        calculator.Enqueue("hash-2");
        calculator.Enqueue("hash-2");
        var service = new SceneTreeLiteSourceRefreshService(reader, store, calculator);

        var result = await service.Refresh(
            CreateProject(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            "Assets/Scenes/Main.unity",
            "readIndex stale.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(firstResponse, result.Response);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal(UnityExecutionMode.Auto, reader.LastMode);
        Assert.Equal(3, calculator.CallCount);
        Assert.Equal(0, store.CallCount);
        Assert.NotNull(result.FallbackReason);
        Assert.Contains("readIndex stale.", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("scene source changed while the snapshot was being read.", result.FallbackReason!, StringComparison.Ordinal);
        Assert.Contains("retry snapshot read failed. retry read timed out", result.FallbackReason!, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_WhenSceneIsOutsideAssets_SkipsPersistence ()
    {
        var reader = new StubSceneTreeLiteSnapshotReader();
        var response = CreateResponse("Packages/com.example/Scenes/Main.unity", "PackageRoot");
        reader.Enqueue(SceneTreeLiteSnapshotFetchResult.Success(response));
        var store = new StubSceneTreeLiteStore();
        var calculator = new StubSceneTreeLiteSourceHashCalculator();
        var service = new SceneTreeLiteSourceRefreshService(reader, store, calculator);

        var result = await service.Refresh(
            CreateProject(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            "Packages/com.example/Scenes/Main.unity",
            "scene-tree-lite readIndex is unavailable for non-Assets scene paths.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(response, result.Response);
        Assert.Equal(1, reader.CallCount);
        Assert.Equal(UnityExecutionMode.Auto, reader.LastMode);
        Assert.Equal(0, calculator.CallCount);
        Assert.Equal(0, store.CallCount);
    }

    private static ResolvedUnityProjectContext CreateProject ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static IpcIndexSceneTreeLiteReadResponse CreateResponse (
        string scenePath,
        string rootName)
    {
        return new IpcIndexSceneTreeLiteReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
            ScenePath: scenePath,
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(rootName, "GlobalObjectId_V1-1-1-1", Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
            ]);
    }

    private sealed class StubSceneTreeLiteSnapshotReader : ISceneTreeLiteSnapshotReader
    {
        private readonly Queue<SceneTreeLiteSnapshotFetchResult> results = new();

        public int CallCount { get; private set; }

        public UnityExecutionMode LastMode { get; private set; }

        public bool LastFailFast { get; private set; }

        public void Enqueue (SceneTreeLiteSnapshotFetchResult result)
        {
            results.Enqueue(result);
        }

        public ValueTask<SceneTreeLiteSnapshotFetchResult> Read (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            string scenePath,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastMode = mode;
            LastFailFast = failFast;
            if (!results.TryDequeue(out var result))
            {
                throw new InvalidOperationException("Scene snapshot result is not configured.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubSceneTreeLiteStore : ISceneTreeLiteStore
    {
        public int CallCount { get; private set; }

        public string? SourceInputsHash { get; private set; }

        public ValueTask Write (
            string storageRoot,
            string projectFingerprint,
            DateTimeOffset generatedAtUtc,
            string scenePath,
            IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
            string sourceInputsHash,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            SourceInputsHash = sourceInputsHash;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubSceneTreeLiteSourceHashCalculator : ISceneTreeLiteSourceHashCalculator
    {
        private readonly Queue<string?> results = new();

        public int CallCount { get; private set; }

        public void Enqueue (string? result)
        {
            results.Enqueue(result);
        }

        public ValueTask<string?> TryCompute (
            string projectRootPath,
            string scenePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (!results.TryDequeue(out var result))
            {
                throw new InvalidOperationException("Scene source hash result is not configured.");
            }

            return ValueTask.FromResult(result);
        }
    }
}
