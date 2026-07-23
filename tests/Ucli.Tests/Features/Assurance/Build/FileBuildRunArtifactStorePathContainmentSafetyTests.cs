using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using static MackySoft.Ucli.Tests.Features.Assurance.Build.FileBuildRunArtifactStoreTestSupport;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

public sealed class FileBuildRunArtifactStorePathContainmentSafetyTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenOutputPathCannotBeRepresentedByPortableContract_FailsClosed ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-backslash-traversal");
        var (store, paths) = PrepareArtifacts(scope);
        var outputSourceDirectory = Path.Combine(paths.RunnerOutputDirectory.Value, "build");
        Directory.CreateDirectory(outputSourceDirectory);
        var literalBackslashPath = Path.Combine(
            outputSourceDirectory,
            "foo\\..\\..\\outside");
        File.WriteAllText(literalBackslashPath, "ambiguous");

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
        Assert.Contains("portable artifact contract", error.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(literalBackslashPath));
        Assert.False(File.Exists(Path.Combine(paths.RunnerOutputDirectory.Value, "outside")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenOutputSourceResolvesInsideArtifactRoot_ReturnsOutputPathInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "source-inside-artifact-root");
        var (store, paths) = PrepareArtifacts(scope);
        WriteUnityGeneratedArtifacts(paths);
        var defaultRequest = CreateAccountingRequest(paths);
        var request = new BuildRunArtifactAccountingRequest(
            defaultRequest.Paths,
            defaultRequest.BuildTarget,
            defaultRequest.UnityBuildTarget,
            defaultRequest.BuildReport,
            [BuildOutputSourceEntry.FromAbsolutePath(AbsolutePath.Parse(
                Path.Combine(paths.ArtifactsDirectory.Value, "source")))],
            defaultRequest.AllowEmptyOutputManifest);

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputPathInvalid, error.Code);
        Assert.Contains("output path", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenOutputSourceResolvesOutsideRunnerOutputRoot_ReturnsOutputPathInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "source-outside-runner-output-root");
        var (store, paths) = PrepareArtifacts(scope);
        WriteUnityGeneratedArtifacts(paths);
        var outsideSourcePath = scope.WriteFile("external-output.bin", "external");
        var defaultRequest = CreateAccountingRequest(paths);
        var request = new BuildRunArtifactAccountingRequest(
            defaultRequest.Paths,
            defaultRequest.BuildTarget,
            defaultRequest.UnityBuildTarget,
            defaultRequest.BuildReport,
            [BuildOutputSourceEntry.FromAbsolutePath(AbsolutePath.Parse(outsideSourcePath))],
            defaultRequest.AllowEmptyOutputManifest);

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputPathInvalid, error.Code);
        Assert.Contains("runner output root", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("buildJson")]
    [InlineData("buildReport")]
    [InlineData("buildLog")]
    [InlineData("outputManifest")]
    [InlineData("artifactOutput")]
    [InlineData("runnerOutput")]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenArtifactPathEscapesLayout_ReturnsInvalidArgumentWithoutWriting (string pathKind)
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", $"path-escape-{pathKind}");
        var (store, paths) = PrepareArtifacts(scope);
        var escapedPath = pathKind is "artifactOutput" or "runnerOutput"
            ? scope.CreateDirectory("escaped-output")
            : Path.Combine(scope.FullPath, $"escaped-{pathKind}.json");
        var request = CreateAccountingRequest(EscapeArtifactPath(paths, pathKind, escapedPath));

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        if (pathKind is not "artifactOutput" and not "runnerOutput")
        {
            Assert.False(File.Exists(escapedPath));
        }

        Assert.False(File.Exists(paths.BuildJsonPath.Value));
        Assert.False(File.Exists(paths.BuildReportJsonPath.Value));
        Assert.False(File.Exists(paths.BuildLogPath.Value));
        Assert.False(File.Exists(paths.OutputManifestJsonPath.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenArtifactDirectoryUsesUnexpectedLayout_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "unexpected-layout");
        var (store, paths) = PrepareArtifacts(scope);
        var artifactsDirectory = scope.CreateDirectory("unexpected-artifacts");
        var unexpectedPaths = new BuildRunArtifactPaths(
            paths.RepositoryRoot,
            paths.RunId,
            AbsolutePath.Parse(artifactsDirectory),
            AbsolutePath.Parse(Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildMetadataFileName)),
            AbsolutePath.Parse(Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildReportFileName)),
            AbsolutePath.Parse(Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildLogFileName)),
            AbsolutePath.Parse(Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildOutputManifestFileName)),
            paths.RunnerOutputDirectory,
            AbsolutePath.Parse(Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildOutputDirectoryName)));
        var request = CreateAccountingRequest(unexpectedPaths);

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }
}
