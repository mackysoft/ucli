using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using static MackySoft.Ucli.Tests.Features.Assurance.Build.FileBuildRunArtifactStoreTestSupport;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

public sealed class FileBuildRunArtifactStorePathContainmentSafetyTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenOutputPathContainsBackslashTraversalText_ReturnsOutputManifestFailed ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-backslash-traversal");
        var (store, paths) = PrepareArtifacts(scope);
        var outputSourceDirectory = Path.Combine(paths.RunnerOutputDirectory, "build");
        Directory.CreateDirectory(outputSourceDirectory);
        File.WriteAllText(Path.Combine(outputSourceDirectory, "foo\\..\\..\\outside"), "ambiguous");

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
        Assert.Contains("escaped", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenOutputSourceResolvesInsideArtifactRoot_ReturnsOutputPathInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "source-inside-artifact-root");
        var (store, paths) = PrepareArtifacts(scope);
        WriteUnityGeneratedArtifacts(paths);
        var request = CreateAccountingRequest(paths) with
        {
            OutputSources = [BuildOutputSourceEntry.FromAbsolutePath(Path.Combine(paths.ArtifactsDirectory, "source"))],
        };

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
        var request = CreateAccountingRequest(paths) with
        {
            OutputSources = [BuildOutputSourceEntry.FromAbsolutePath(outsideSourcePath)],
        };

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

        Assert.False(File.Exists(paths.BuildJsonPath));
        Assert.False(File.Exists(paths.BuildReportJsonPath));
        Assert.False(File.Exists(paths.BuildLogPath));
        Assert.False(File.Exists(paths.OutputManifestJsonPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenArtifactDirectoryUsesUnexpectedLayout_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "unexpected-layout");
        var (store, paths) = PrepareArtifacts(scope);
        var artifactsDirectory = scope.CreateDirectory("unexpected-artifacts");
        var request = CreateAccountingRequest(paths with
        {
            ArtifactsDirectory = artifactsDirectory,
            BuildJsonPath = Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildMetadataFileName),
            BuildReportJsonPath = Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildReportFileName),
            BuildLogPath = Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildLogFileName),
            OutputManifestJsonPath = Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildOutputManifestFileName),
            ArtifactOutputDirectory = Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildOutputDirectoryName),
        });

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }
}
