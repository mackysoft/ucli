using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
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
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");

        var result = store.Prepare(project, "run-1");

        Assert.True(result.IsSuccess);
        var paths = Assert.IsType<BuildRunArtifactPaths>(result.Paths);
        Assert.True(Directory.Exists(paths.ArtifactsDirectory));
        Assert.True(Directory.Exists(paths.RunnerOutputDirectory));
        Assert.False(Directory.Exists(paths.ArtifactOutputDirectory));
        Assert.Equal(
            Path.Combine(
                scope.FullPath,
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "fingerprint",
                UcliStoragePathNames.ArtifactsDirectoryName,
                UcliStoragePathNames.BuildArtifactsDirectoryName,
                "run-1"),
            paths.ArtifactsDirectory);
        Assert.Equal(
            Path.Combine(
                scope.FullPath,
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "fingerprint",
                UcliStoragePathNames.WorkDirectoryName,
                UcliStoragePathNames.BuildWorkDirectoryName,
                "run-1",
                UcliStoragePathNames.BuildOutputDirectoryName),
            paths.RunnerOutputDirectory);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Prepare_WhenBuildRunArtifactDirectoryAlreadyExists_ReturnsBuildArtifactWriteFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-existing-output");
        var store = CreateStore();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");

        var firstResult = store.Prepare(project, "run-1");
        var secondResult = store.Prepare(project, "run-1");

        Assert.True(firstResult.IsSuccess);
        Assert.False(secondResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(secondResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Prepare_WhenBuildRunArtifactDirectoryContainsLegacyArtifact_ReturnsBuildArtifactWriteFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-existing-legacy");
        var store = CreateStore();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var artifactsDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            "run-1");
        Directory.CreateDirectory(artifactsDirectory);
        File.WriteAllText(Path.Combine(artifactsDirectory, "build-summary.json"), "{}");

        var result = store.Prepare(project, "run-1");

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void PrepareBuildPipelineOutputLayout_WithResolvedLayout_CreatesPlayerParentDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-player-layout");
        var (store, paths) = PrepareArtifacts(scope);
        Assert.True(IpcBuildOutputLayoutResolver.TryResolve(paths.RunnerOutputDirectory, "standaloneLinux64", out var layout));

        var result = store.PrepareBuildPipelineOutputLayout(paths, "standaloneLinux64", layout!);

        Assert.True(result.IsSuccess);
        Assert.True(Directory.Exists(Path.GetDirectoryName(layout!.LocationPathName)));
        Assert.False(File.Exists(layout.LocationPathName));
        Assert.False(Directory.Exists(layout.LocationPathName));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void PrepareBuildPipelineOutputLayout_WhenLocationPathNameAlreadyExists_ReturnsBuildArtifactWriteFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-player-collision");
        var (store, paths) = PrepareArtifacts(scope);
        Assert.True(IpcBuildOutputLayoutResolver.TryResolve(paths.RunnerOutputDirectory, "standaloneLinux64", out var layout));
        WriteUtf8(layout!.LocationPathName, "existing player");

        var result = store.PrepareBuildPipelineOutputLayout(paths, "standaloneLinux64", layout);

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
        Assert.True(IpcBuildOutputLayoutResolver.TryResolve(paths.RunnerOutputDirectory, "standaloneLinux64", out var layout));
        WriteUtf8(Path.Combine(paths.RunnerOutputDirectory, "player"), "blocking file");

        var result = store.PrepareBuildPipelineOutputLayout(paths, "standaloneLinux64", layout!);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void PrepareBuildPipelineOutputLayout_WhenTargetLayoutIsUnsupported_ReturnsBuildInputsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-player-unsupported");
        var (store, paths) = PrepareArtifacts(scope);
        var layout = new IpcBuildOutputLayout(
            Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
            LocationPathName: Path.Combine(paths.RunnerOutputDirectory, "player", "Player"));

        var result = store.PrepareBuildPipelineOutputLayout(paths, "switch", layout);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildInputsInvalid, error.Code);
    }
}
