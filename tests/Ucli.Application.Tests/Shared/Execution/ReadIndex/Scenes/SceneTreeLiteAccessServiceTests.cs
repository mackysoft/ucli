using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes;

public sealed class SceneTreeLiteAccessServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenAllowStaleLookupExists_ReturnsTrimmedIndexRoots ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "index-depth-one");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new StubIndexCatalogReader
        {
            SceneTreeLiteLookupResult = ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Success(
                new IndexSceneTreeLiteLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    SourceInputsHash: "scene-hash",
                    Roots: CreateTree())),
        };
        var freshnessEvaluator = new StubSceneTreeLiteFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Probable),
        };
        var refreshService = new StubSceneTreeLiteSourceRefreshService();
        var service = new SceneTreeLiteAccessService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), refreshService, new StubSceneTreeLiteSourceProbe());

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            "Assets/Scenes/Main.unity",
            depth: 1,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Index, result.Output!.AccessInfo.Source);
        Assert.True(result.Output.AccessInfo.Used);
        Assert.Equal(IndexFreshness.Probable, result.Output.AccessInfo.Freshness);
        Assert.Single(result.Output.Roots);
        Assert.Single(result.Output.Roots[0].Children!);
        Assert.Empty(result.Output.Roots[0].Children![0].Children!);
        Assert.Equal(0, refreshService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenDepthIsZero_RemovesAllChildren ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "depth-zero");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new StubIndexCatalogReader
        {
            SceneTreeLiteLookupResult = ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Success(
                new IndexSceneTreeLiteLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    SourceInputsHash: "scene-hash",
                    Roots: CreateTree())),
        };
        var freshnessEvaluator = new StubSceneTreeLiteFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh),
        };
        var service = new SceneTreeLiteAccessService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), new StubSceneTreeLiteSourceRefreshService(), new StubSceneTreeLiteSourceProbe());

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.RequireFresh,
            "Assets/Scenes/Main.unity",
            depth: 0,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Output!.Roots[0].Children!);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenDepthIsNull_ReturnsFullTree ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "depth-null");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new StubIndexCatalogReader
        {
            SceneTreeLiteLookupResult = ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Success(
                new IndexSceneTreeLiteLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    SourceInputsHash: "scene-hash",
                    Roots: CreateTree())),
        };
        var freshnessEvaluator = new StubSceneTreeLiteFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh),
        };
        var service = new SceneTreeLiteAccessService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), new StubSceneTreeLiteSourceRefreshService(), new StubSceneTreeLiteSourceProbe());

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.RequireFresh,
            "Assets/Scenes/Main.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Output!.Roots[0].Children!);
        Assert.Single(result.Output.Roots[0].Children![0].Children!);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenRequireFreshLookupIsStale_FallsBackToSource ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "stale-fallback");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new StubIndexCatalogReader
        {
            SceneTreeLiteLookupResult = ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Success(
                new IndexSceneTreeLiteLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    SourceInputsHash: "stale-hash",
                    Roots: CreateTree())),
        };
        var freshnessEvaluator = new StubSceneTreeLiteFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Failure(
                IndexFreshness.Stale,
                new IndexServiceError(IpcErrorCodes.ReadIndexFreshRequired, "fresh required")),
        };
        var refreshService = new StubSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:01:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract("FreshRoot", "GlobalObjectId_V1-1-1-1", Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
                    ]),
                "Existing scene-tree-lite index freshness is 'stale'."),
        };
        var service = new SceneTreeLiteAccessService(indexReader, freshnessEvaluator, new TestMutationReadPostconditionStore(), refreshService, new StubSceneTreeLiteSourceProbe());

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.RequireFresh,
            "Assets/Scenes/Main.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output!.AccessInfo.Source);
        Assert.Contains("stale", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Equal(1, refreshService.CallCount);
        Assert.Equal(UnityExecutionMode.Auto, refreshService.LastMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenReadPostconditionRequiresNewerSceneIndex_FallsBackToSource ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "postcondition-fallback");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new StubIndexCatalogReader
        {
            SceneTreeLiteLookupResult = ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Success(
                new IndexSceneTreeLiteLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    SourceInputsHash: "scene-hash",
                    Roots: CreateTree())),
        };
        var freshnessEvaluator = new StubSceneTreeLiteFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh),
        };
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            ReadResult = MutationReadPostconditionReadResult.Success(
                ReadPostconditionTestFactory.Create(
                [
                    new IpcExecuteReadPostconditionRequirement(
                        Surface: IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite,
                        MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-15T00:00:00+00:00"))
                    {
                        ScenePath = "Assets/Scenes/Main.unity",
                    },
                ])),
        };
        var refreshService = new StubSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-15T00:01:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract("FreshRoot", "GlobalObjectId_V1-1-1-1", Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
                    ]),
                "Existing scene-tree-lite index generatedAtUtc is older than mutation read postcondition."),
        };
        var service = new SceneTreeLiteAccessService(indexReader, freshnessEvaluator, readPostconditionStore, refreshService, new StubSceneTreeLiteSourceProbe());

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            "Assets/Scenes/Main.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output!.AccessInfo.Source);
        Assert.Contains("mutation read postcondition", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenReadPostconditionTargetsDifferentScene_KeepsUsingIndex ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "postcondition-non-matching-scene");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new StubIndexCatalogReader
        {
            SceneTreeLiteLookupResult = ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Success(
                new IndexSceneTreeLiteLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    SourceInputsHash: "scene-hash",
                    Roots: CreateTree())),
        };
        var freshnessEvaluator = new StubSceneTreeLiteFreshnessEvaluator
        {
            Result = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh),
        };
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            ReadResult = MutationReadPostconditionReadResult.Success(
                ReadPostconditionTestFactory.Create(
                [
                    new IpcExecuteReadPostconditionRequirement(
                        Surface: IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite,
                        MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-15T00:00:00+00:00"))
                    {
                        ScenePath = "Assets/Scenes/Other.unity",
                    },
                ])),
        };
        var refreshService = new StubSceneTreeLiteSourceRefreshService();
        var service = new SceneTreeLiteAccessService(indexReader, freshnessEvaluator, readPostconditionStore, refreshService, new StubSceneTreeLiteSourceProbe());

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.RequireFresh,
            "Assets/Scenes/Main.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Index, result.Output!.AccessInfo.Source);
        Assert.Equal(0, refreshService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenReadIndexModeIsDisabled_UsesSourcePath ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "disabled");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var refreshService = new StubSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:01:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots: CreateTree()),
                "readIndex disabled by mode."),
        };
        var indexReader = new StubIndexCatalogReader();
        var service = new SceneTreeLiteAccessService(indexReader, new StubSceneTreeLiteFreshnessEvaluator(), new TestMutationReadPostconditionStore(), refreshService, new StubSceneTreeLiteSourceProbe());

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.Disabled,
            "Assets/Scenes/Main.unity",
            depth: null,
            failFast: true,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output!.AccessInfo.Source);
        Assert.Equal("readIndex disabled by mode.", result.Output.AccessInfo.FallbackReason);
        Assert.Equal(0, indexReader.SceneTreeLiteLookupCallCount);
        Assert.Equal(UnityExecutionMode.Auto, refreshService.LastMode);
        Assert.True(refreshService.LastFailFast);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenSceneIsOutsideAssets_UsesLiveOnlySource ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "package-live-only");
        var project = CreateProject(scope);
        var refreshService = new StubSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:01:00+00:00"),
                    ScenePath: "Packages/com.example/Scenes/Main.unity",
                    Roots: CreateTree()),
                "scene-tree-lite readIndex is unavailable for non-Assets scene paths."),
        };
        var indexReader = new StubIndexCatalogReader();
        var service = new SceneTreeLiteAccessService(indexReader, new StubSceneTreeLiteFreshnessEvaluator(), new TestMutationReadPostconditionStore(), refreshService, new StubSceneTreeLiteSourceProbe());

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            "Packages/com.example/Scenes/Main.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output!.AccessInfo.Source);
        Assert.Contains("non-Assets", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Equal(0, indexReader.SceneTreeLiteLookupCallCount);
        Assert.Equal(UnityExecutionMode.Auto, refreshService.LastMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenAssetsSceneIsMissing_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "missing-scene");
        var project = CreateProject(scope);
        var indexReader = new StubIndexCatalogReader
        {
            SceneTreeLiteLookupResult = ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Success(
                new IndexSceneTreeLiteLookupJsonContract(
                    SchemaVersion: 1,
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    SourceInputsHash: "scene-hash",
                    Roots: CreateTree())),
        };
        var service = new SceneTreeLiteAccessService(
            indexReader,
            new StubSceneTreeLiteFreshnessEvaluator(),
            new TestMutationReadPostconditionStore(),
            new StubSceneTreeLiteSourceRefreshService(),
            new StubSceneTreeLiteSourceProbe(SceneTreeLiteSourceProbeResult.Failure("Scene path could not be resolved: Assets/Scenes/Main.unity")));

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            "Assets/Scenes/Main.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Contains("Scene path could not be resolved", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, indexReader.SceneTreeLiteLookupCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenScenePathContainsTraversal_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "traversal-scene");
        var project = CreateProject(scope);
        var indexReader = new StubIndexCatalogReader();
        var service = new SceneTreeLiteAccessService(indexReader, new StubSceneTreeLiteFreshnessEvaluator(), new TestMutationReadPostconditionStore(), new StubSceneTreeLiteSourceRefreshService(), new StubSceneTreeLiteSourceProbe());

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            "Assets/../Outside.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Contains("project-relative path", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, indexReader.SceneTreeLiteLookupCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenScenePathIsWindowsRooted_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "windows-rooted-scene");
        var project = CreateProject(scope);
        var indexReader = new StubIndexCatalogReader();
        var service = new SceneTreeLiteAccessService(indexReader, new StubSceneTreeLiteFreshnessEvaluator(), new TestMutationReadPostconditionStore(), new StubSceneTreeLiteSourceRefreshService(), new StubSceneTreeLiteSourceProbe());

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            @"C:\repo\Project\Assets\Scenes\Main.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Contains("project-relative path", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, indexReader.SceneTreeLiteLookupCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenLookupIsMissing_FallsBackToSourceWithRequestedMode ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "missing-lookup");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new StubIndexCatalogReader
        {
            SceneTreeLiteLookupResult = ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Failure(
                IpcErrorCodes.ReadIndexBootstrapFailed,
                "scene-tree-lite lookup is missing."),
        };
        var refreshService = new StubSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:01:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots: CreateTree()),
                "scene-tree-lite lookup is missing."),
        };
        var service = new SceneTreeLiteAccessService(indexReader, new StubSceneTreeLiteFreshnessEvaluator(), new TestMutationReadPostconditionStore(), refreshService, new StubSceneTreeLiteSourceProbe());

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            "Assets/Scenes/Main.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output!.AccessInfo.Source);
        Assert.Equal(UnityExecutionMode.Auto, refreshService.LastMode);
        Assert.Contains("missing", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenLookupIsMalformed_FallsBackToSource ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-access", "malformed-lookup");
        var project = CreateProject(scope);
        WriteSceneFile(project.UnityProjectRoot, "Assets/Scenes/Main.unity");
        var indexReader = new StubIndexCatalogReader
        {
            SceneTreeLiteLookupResult = ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Failure(
                IpcErrorCodes.ReadIndexFormatInvalid,
                "Index contract file 'lookups/scene-tree-lite/*.lookup.json' is malformed."),
        };
        var refreshService = new StubSceneTreeLiteSourceRefreshService
        {
            Result = SceneTreeLiteRefreshResult.Success(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:01:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots: CreateTree()),
                "Index contract file 'lookups/scene-tree-lite/*.lookup.json' is malformed."),
        };
        var service = new SceneTreeLiteAccessService(indexReader, new StubSceneTreeLiteFreshnessEvaluator(), new TestMutationReadPostconditionStore(), refreshService, new StubSceneTreeLiteSourceProbe());

        var result = await service.Read(
            project,
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            ReadIndexMode.AllowStale,
            "Assets/Scenes/Main.unity",
            depth: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("malformed", result.Output!.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output.AccessInfo.Source);
        Assert.Equal(UnityExecutionMode.Auto, refreshService.LastMode);
    }

    private static ResolvedUnityProjectContext CreateProject (TestDirectoryScope scope)
    {
        var projectRoot = scope.CreateDirectory("UnityProject");
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: projectRoot,
            RepositoryRoot: scope.FullPath,
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static void WriteSceneFile (
        string projectRootPath,
        string scenePath)
    {
        var absolutePath = Path.Combine(projectRootPath, scenePath.Replace('/', Path.DirectorySeparatorChar));
        var directoryPath = Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {absolutePath}");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(absolutePath, "scene");
    }

    private static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> CreateTree ()
    {
        return
        [
            new IndexSceneTreeLiteNodeJsonContract(
                "Root",
                "GlobalObjectId_V1-1-1-1",
                [
                    new IndexSceneTreeLiteNodeJsonContract(
                        "Child",
                        "GlobalObjectId_V1-1-1-2",
                        [
                            new IndexSceneTreeLiteNodeJsonContract(
                                "Grandchild",
                                "GlobalObjectId_V1-1-1-3",
                                Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
                        ]),
                ]),
        ];
    }

    private sealed class StubIndexCatalogReader : IReadIndexArtifactReader
    {
        public int SceneTreeLiteLookupCallCount { get; private set; }

        public ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract> SceneTreeLiteLookupResult { get; set; }
            = ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Failure(IpcErrorCodes.ReadIndexBootstrapFailed, "missing");

        public ValueTask<ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>> ReadInputsManifest (string storageRoot, string projectFingerprint, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookup (
            string storageRoot,
            string projectFingerprint,
            string scenePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SceneTreeLiteLookupCallCount++;
            return ValueTask.FromResult(SceneTreeLiteLookupResult);
        }
    }

    private sealed class StubSceneTreeLiteFreshnessEvaluator : IReadIndexFreshnessEvaluator
    {
        public IndexFreshnessEvaluationResult Result { get; set; }
            = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh);

        public ValueTask<IndexFreshnessEvaluationResult> Evaluate (
            string projectRootPath,
            IndexFreshnessTarget target,
            string? persistedSourceInputsHash,
            ReadIndexMode mode,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IndexFreshnessEvaluationResult> EvaluateSceneTreeLite (
            string projectRootPath,
            string scenePath,
            string? persistedSourceInputsHash,
            ReadIndexMode mode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubSceneTreeLiteSourceProbe : ISceneTreeLiteSourceProbe
    {
        private readonly SceneTreeLiteSourceProbeResult result;

        public StubSceneTreeLiteSourceProbe ()
            : this(SceneTreeLiteSourceProbeResult.Success())
        {
        }

        public StubSceneTreeLiteSourceProbe (SceneTreeLiteSourceProbeResult result)
        {
            this.result = result;
        }

        public ValueTask<SceneTreeLiteSourceProbeResult> EnsureCurrentAssetsSceneExists (
            ResolvedUnityProjectContext project,
            string scenePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubSceneTreeLiteSourceRefreshService : ISceneTreeLiteSourceRefreshService
    {
        public int CallCount { get; private set; }

        public UnityExecutionMode LastMode { get; private set; }

        public bool LastFailFast { get; private set; }

        public SceneTreeLiteRefreshResult Result { get; set; }
            = SceneTreeLiteRefreshResult.Failure("not configured", IpcErrorCodes.InternalError);

        public ValueTask<SceneTreeLiteRefreshResult> Refresh (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            ReadIndexMode readIndexMode,
            string scenePath,
            string fallbackReason,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastMode = mode;
            LastFailFast = failFast;
            return ValueTask.FromResult(Result);
        }
    }
}
