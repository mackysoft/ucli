using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes.SceneTreeLiteAccessServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes;

public sealed class SceneTreeLiteAccessServiceSourceFallbackTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenRequireFreshLookupIsStale_FallsBackToSource ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "stale-fallback");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new RecordingReadIndexArtifactReader
        {
            SceneTreeLiteLookupResult = CreateSuccessfulSceneTreeLiteLookupReadResult(
                sourceInputsHash: Sha256DigestTestFactory.Compute("stale-hash")),
        };
        var freshnessEvaluator = new RecordingReadIndexFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Stale),
        };
        var refreshService = new RecordingSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                CreateSceneTreeLiteSourceSnapshot(
                    generatedAtUtc: DateTimeOffset.Parse("2026-04-14T00:01:00+00:00"),
                    scenePath: "Assets/Scenes/Main.unity",
                    roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract("FreshRoot", "GlobalObjectId_V1-2-11111111111111111111111111111111-1-0", Array.Empty<IndexSceneTreeLiteNodeJsonContract>(), IndexSceneTreeLiteNodeChildrenState.Complete),
                    ],
                    sourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.PersistedPreview, isDirty: false)),
                "Existing scene-tree-lite index freshness is 'stale'."),
        };
        var service = CreateService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), refreshService, new RecordingSceneTreeLiteSourceProbe());

        var result = await service.ReadAsync(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.RequireFresh,
            new UnityScenePath("Assets/Scenes/Main.unity"),
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output!.AccessInfo.Source);
        Assert.Contains("stale", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        SceneTreeLiteAccessInvocationAssert.SourceRefreshAttemptedFor(
            refreshService,
            project,
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            "Assets/Scenes/Main.unity");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenReadIndexModeIsDisabled_UsesSourcePath ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "disabled");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var refreshService = new RecordingSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                CreateSceneTreeLiteSourceSnapshot(
                    generatedAtUtc: DateTimeOffset.Parse("2026-04-14T00:01:00+00:00"),
                    scenePath: "Assets/Scenes/Main.unity",
                    roots: CreateTree(),
                    sourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.PersistedPreview, isDirty: false)),
                "readIndex disabled by mode."),
        };
        var indexReader = new RecordingReadIndexArtifactReader();
        var service = CreateService(indexReader, new RecordingReadIndexFreshnessEvaluator(), new TestMutationReadPostconditionStore(), refreshService, new RecordingSceneTreeLiteSourceProbe());

        var result = await service.ReadAsync(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.Disabled,
            new UnityScenePath("Assets/Scenes/Main.unity"),
            depth: null,
            failFast: true,
            cancellationToken: CancellationToken.None);

        SceneTreeLiteAccessInvocationAssert.SourceRefreshReturnedWithoutIndexLookup(
            result,
            indexReader,
            refreshService,
            project,
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            "Assets/Scenes/Main.unity",
            "readIndex disabled by mode.",
            expectedFailFast: true);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenSceneIsOutsideAssets_UsesLiveOnlySource ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "package-live-only");
        var project = CreateProject(scope);
        var refreshService = new RecordingSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                CreateSceneTreeLiteSourceSnapshot(
                    generatedAtUtc: DateTimeOffset.Parse("2026-04-14T00:01:00+00:00"),
                    scenePath: "Packages/com.example/Scenes/Main.unity",
                    roots: CreateTree(),
                    sourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.PersistedPreview, isDirty: false)),
                "scene-tree-lite readIndex is unavailable for non-Assets scene paths."),
        };
        var indexReader = new RecordingReadIndexArtifactReader();
        var service = CreateService(indexReader, new RecordingReadIndexFreshnessEvaluator(), new TestMutationReadPostconditionStore(), refreshService, new RecordingSceneTreeLiteSourceProbe());

        var result = await service.ReadAsync(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.RequireFresh,
            new UnityScenePath("Packages/com.example/Scenes/Main.unity"),
            depth: null,
            cancellationToken: CancellationToken.None);

        SceneTreeLiteAccessInvocationAssert.SourceRefreshReturnedWithoutIndexLookup(
            result,
            indexReader,
            refreshService,
            project,
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            "Packages/com.example/Scenes/Main.unity",
            "non-Assets");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenLookupIsMissing_FallsBackToSourceWithRequestedMode ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "missing-lookup");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new RecordingReadIndexArtifactReader
        {
            SceneTreeLiteLookupResult = ReadIndexArtifactReadResult<SceneTreeLiteLookupSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                "scene-tree-lite lookup is missing."),
        };
        var refreshService = new RecordingSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                CreateSceneTreeLiteSourceSnapshot(
                    generatedAtUtc: DateTimeOffset.Parse("2026-04-14T00:01:00+00:00"),
                    scenePath: "Assets/Scenes/Main.unity",
                    roots: CreateTree(),
                    sourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.PersistedPreview, isDirty: false)),
                "scene-tree-lite lookup is missing."),
        };
        var service = CreateService(indexReader, new RecordingReadIndexFreshnessEvaluator(), new TestMutationReadPostconditionStore(), refreshService, new RecordingSceneTreeLiteSourceProbe());

        var result = await service.ReadAsync(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.RequireFresh,
            new UnityScenePath("Assets/Scenes/Main.unity"),
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output!.AccessInfo.Source);
        SceneTreeLiteAccessInvocationAssert.SourceRefreshAttemptedFor(
            refreshService,
            project,
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            "Assets/Scenes/Main.unity");
        Assert.Contains("missing", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenLookupIsMalformed_FallsBackToSource ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "malformed-lookup");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new RecordingReadIndexArtifactReader
        {
            SceneTreeLiteLookupResult = ReadIndexArtifactReadResult<SceneTreeLiteLookupSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                "Index contract file 'lookups/scene-tree-lite/*.lookup.json' is malformed."),
        };
        var refreshService = new RecordingSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                CreateSceneTreeLiteSourceSnapshot(
                    generatedAtUtc: DateTimeOffset.Parse("2026-04-14T00:01:00+00:00"),
                    scenePath: "Assets/Scenes/Main.unity",
                    roots: CreateTree(),
                    sourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.PersistedPreview, isDirty: false)),
                "Index contract file 'lookups/scene-tree-lite/*.lookup.json' is malformed."),
        };
        var service = CreateService(indexReader, new RecordingReadIndexFreshnessEvaluator(), new TestMutationReadPostconditionStore(), refreshService, new RecordingSceneTreeLiteSourceProbe());

        var result = await service.ReadAsync(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.RequireFresh,
            new UnityScenePath("Assets/Scenes/Main.unity"),
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("malformed", result.Output!.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output.AccessInfo.Source);
        SceneTreeLiteAccessInvocationAssert.SourceRefreshAttemptedFor(
            refreshService,
            project,
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            "Assets/Scenes/Main.unity");
    }
}
