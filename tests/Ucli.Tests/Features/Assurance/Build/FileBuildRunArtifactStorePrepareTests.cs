using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using static MackySoft.Ucli.Tests.Features.Assurance.Build.FileBuildRunArtifactStoreTestSupport;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

public sealed class FileBuildRunArtifactStorePrepareTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Prepare_CreatesBuildRunArtifactLayout ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-layout");
        var store = CreateStore();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));

        var result = store.Prepare(project, RunIdTestValues.Build);

        Assert.True(result.IsSuccess);
        var paths = Assert.IsType<BuildRunArtifactPaths>(result.Paths);
        Assert.True(Directory.Exists(paths.ArtifactsDirectory.Value));
        Assert.True(Directory.Exists(paths.RunnerOutputDirectory.Value));
        Assert.False(Directory.Exists(paths.ArtifactOutputDirectory.Value));
        Assert.Equal(
            UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
                AbsolutePath.Parse(scope.FullPath),
                RunIdTestValues.Build),
            paths.ArtifactsDirectory);
        Assert.Equal(
            UcliStoragePathResolver.ResolveBuildRunOutputDirectory(
                AbsolutePath.Parse(scope.FullPath),
                RunIdTestValues.Build),
            paths.RunnerOutputDirectory);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Prepare_WithEmptyRunId_ReturnsInvalidArgumentWithoutCreatingStorage ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-empty-run-id");
        var store = CreateStore();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));

        var result = store.Prepare(project, Guid.Empty);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.False(Directory.Exists(Path.Combine(scope.FullPath, UcliStoragePathNames.UcliDirectoryName)));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Prepare_WhenBuildRunArtifactDirectoryAlreadyExists_ReturnsBuildArtifactWriteFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-existing-output");
        var store = CreateStore();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));

        var firstResult = store.Prepare(project, RunIdTestValues.Build);
        var secondResult = store.Prepare(project, RunIdTestValues.Build);

        Assert.True(firstResult.IsSuccess);
        Assert.False(secondResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(secondResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Prepare_WhenSameRunIsPreparedConcurrently_GrantsExactlyOneOwner ()
    {
        const int contenderCount = 8;
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-concurrent");
        var store = CreateStore();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        using var startBarrier = new Barrier(contenderCount);
        var tasks = Enumerable
            .Range(0, contenderCount)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    startBarrier.SignalAndWait();
                    return store.Prepare(project, RunIdTestValues.Build);
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.Single(results, static result => result.IsSuccess);
        foreach (var failure in results.Where(static result => !result.IsSuccess))
        {
            var error = Assert.IsType<ExecutionError>(failure.Error);
            Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
            Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Prepare_WhenBuildRunDirectoryContainsUnknownEntry_ReturnsBuildArtifactWriteFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-existing-legacy");
        var store = CreateStore();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var runDirectory = UcliStoragePathResolver.ResolveBuildRunDirectory(
            project.RepositoryRoot,
            RunIdTestValues.Build);
        Directory.CreateDirectory(runDirectory.Value);
        var unknownEntryPath = Path.Combine(runDirectory.Value, "unknown-entry");
        File.WriteAllText(unknownEntryPath, "unknown");

        var result = store.Prepare(project, RunIdTestValues.Build);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
        Assert.True(File.Exists(unknownEntryPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Prepare_WhenEmptyBuildRunDirectoryAlreadyExists_ReturnsBuildArtifactWriteFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-existing-empty-run");
        var store = CreateStore();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var runDirectory = UcliStoragePathResolver.ResolveBuildRunDirectory(
            project.RepositoryRoot,
            RunIdTestValues.Build);
        Directory.CreateDirectory(runDirectory.Value);

        var result = store.Prepare(project, RunIdTestValues.Build);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(runDirectory.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void PrepareBuildPipelineOutputLayout_WithResolvedLayout_CreatesPlayerParentDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-player-layout");
        var (store, paths) = PrepareArtifacts(scope);
        Assert.True(BuildPipelineOutputLayoutResolver.TryResolve(
            paths.RunnerOutputDirectory,
            BuildTargetStableName.StandaloneLinux64,
            androidAppBundle: false,
            out var layout));

        var result = store.PrepareBuildPipelineOutputLayout(paths, layout!);

        Assert.True(result.IsSuccess);
        Assert.True(Directory.Exists(Path.GetDirectoryName(layout!.LocationPath.Value)));
        Assert.False(File.Exists(layout.LocationPath.Value));
        Assert.False(Directory.Exists(layout.LocationPath.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void PrepareBuildPipelineOutputLayout_WhenLocationPathNameAlreadyExists_ReturnsBuildArtifactWriteFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-player-collision");
        var (store, paths) = PrepareArtifacts(scope);
        Assert.True(BuildPipelineOutputLayoutResolver.TryResolve(
            paths.RunnerOutputDirectory,
            BuildTargetStableName.StandaloneLinux64,
            androidAppBundle: false,
            out var layout));
        WriteUtf8(layout!.LocationPath.Value, "existing player");

        var result = store.PrepareBuildPipelineOutputLayout(paths, layout);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void PrepareBuildPipelineOutputLayout_WhenPlayerParentCannotBeCreated_ReturnsBuildArtifactWriteFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-player-parent-blocked");
        var (store, paths) = PrepareArtifacts(scope);
        Assert.True(BuildPipelineOutputLayoutResolver.TryResolve(
            paths.RunnerOutputDirectory,
            BuildTargetStableName.StandaloneLinux64,
            androidAppBundle: false,
            out var layout));
        WriteUtf8(Path.Combine(paths.RunnerOutputDirectory.Value, "player"), "blocking file");

        var result = store.PrepareBuildPipelineOutputLayout(paths, layout!);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void PrepareBuildPipelineOutputLayout_WhenLocationIsOutsideRunnerOutput_ReturnsBuildInputsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-player-unsupported");
        var (store, paths) = PrepareArtifacts(scope);
        Assert.True(BuildPipelineOutputLayoutResolver.TryResolve(
            paths.RunnerOutputDirectory,
            BuildTargetStableName.StandaloneLinux64,
            androidAppBundle: false,
            out var layout));

        var outsideLayout = new BuildPipelineOutputLayout(
            layout!.Shape,
            AbsolutePath.Parse(Path.Combine(scope.FullPath, "outside-player")));

        var result = store.PrepareBuildPipelineOutputLayout(paths, outsideLayout);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildInputsInvalid, error.Code);
    }
}
