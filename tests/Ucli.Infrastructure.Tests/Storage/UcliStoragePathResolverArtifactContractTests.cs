using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class UcliStoragePathResolverArtifactContractTests
{
    private static readonly UcliStoragePathResolverTestSupport.RunScopedPathResolverCase[] RunScopedPathResolvers =
    [
        new(
            nameof(UcliStoragePathResolver.ResolveTestRunArtifactsDirectory),
            static (storageRoot, projectFingerprint, runId) =>
                UcliStoragePathResolver.ResolveTestRunArtifactsDirectory(storageRoot, projectFingerprint, runId)),
        new(
            nameof(UcliStoragePathResolver.ResolveCompileRunArtifactsDirectory),
            static (storageRoot, projectFingerprint, runId) =>
                UcliStoragePathResolver.ResolveCompileRunArtifactsDirectory(storageRoot, projectFingerprint, runId)),
        new(
            nameof(UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory),
            static (storageRoot, projectFingerprint, runId) =>
                UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(storageRoot, projectFingerprint, runId)),
        new(
            nameof(UcliStoragePathResolver.ResolveBuildRunOutputDirectory),
            static (storageRoot, projectFingerprint, runId) =>
                UcliStoragePathResolver.ResolveBuildRunOutputDirectory(storageRoot, projectFingerprint, runId)),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveArtifactsDirectory_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveArtifactsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.ArtifactsDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTestArtifactsDirectory_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveTestArtifactsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.ArtifactsDirectoryName,
            UcliStoragePathNames.TestArtifactsDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTestRunArtifactsDirectory_ReturnsRunScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveTestRunArtifactsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            UcliStoragePathResolverTestSupport.RunId);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.ArtifactsDirectoryName,
            UcliStoragePathNames.TestArtifactsDirectoryName,
            UcliStoragePathResolverTestSupport.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveCompileArtifactsDirectory_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveCompileArtifactsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.ArtifactsDirectoryName,
            UcliStoragePathNames.CompileArtifactsDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveCompileRunArtifactsDirectory_ReturnsRunScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveCompileRunArtifactsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            UcliStoragePathResolverTestSupport.RunId);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.ArtifactsDirectoryName,
            UcliStoragePathNames.CompileArtifactsDirectoryName,
            UcliStoragePathResolverTestSupport.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveBuildArtifactsDirectory_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveBuildArtifactsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.ArtifactsDirectoryName,
            UcliStoragePathNames.BuildArtifactsDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveBuildRunArtifactsDirectory_ReturnsRunScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            UcliStoragePathResolverTestSupport.RunId);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.ArtifactsDirectoryName,
            UcliStoragePathNames.BuildArtifactsDirectoryName,
            UcliStoragePathResolverTestSupport.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveBuildRunOutputDirectory_ReturnsRunScopedWorkPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveBuildRunOutputDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            UcliStoragePathResolverTestSupport.RunId);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.WorkDirectoryName,
            UcliStoragePathNames.BuildWorkDirectoryName,
            UcliStoragePathResolverTestSupport.RunId,
            UcliStoragePathNames.BuildOutputDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveScreenshotCapturePaths_ReturnCaptureScopedArtifactAndStagingFiles ()
    {
        const string captureId = "20260711_120000Z_deadbeef";

        var artifactPath = UcliStoragePathResolver.ResolveScreenshotCaptureArtifactPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            captureId);
        var stagingPath = UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            captureId);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            artifactPath,
            UcliStoragePathNames.ArtifactsDirectoryName,
            UcliStoragePathNames.ScreenshotDirectoryName,
            captureId,
            UcliStoragePathNames.ScreenshotPngFileName);
        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            stagingPath,
            UcliStoragePathNames.WorkDirectoryName,
            UcliStoragePathNames.ScreenshotDirectoryName,
            captureId,
            UcliStoragePathNames.ScreenshotRawStagingFileName);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("nested/capture")]
    [InlineData("nested\\capture")]
    [InlineData("capture:alternate")]
    [InlineData("capture id")]
    [InlineData("capture\t")]
    [InlineData(".")]
    [InlineData("..")]
    [Trait("Size", "Small")]
    public void ResolveScreenshotCapturePaths_WithUnsafeCaptureId_ThrowsArgumentException (string captureId)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            UcliStoragePathResolver.ResolveScreenshotCaptureArtifactPath(
                UcliStoragePathResolverTestSupport.StorageRoot,
                UcliStoragePathResolverTestSupport.ProjectFingerprint,
                captureId));

        Assert.Equal("captureId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RunScopedPathResolvers_WithPathSegmentOrTraversalRunId_ThrowArgumentException ()
    {
        foreach (var resolverCase in RunScopedPathResolvers)
        {
            foreach (var runId in UcliStoragePathResolverTestSupport.UnsafeRunIds)
            {
                var exception = Assert.Throws<ArgumentException>(() =>
                    resolverCase.Resolve(
                        UcliStoragePathResolverTestSupport.StorageRoot,
                        UcliStoragePathResolverTestSupport.ProjectFingerprint,
                        runId));

                Assert.True(
                    string.Equals("runId", exception.ParamName, StringComparison.Ordinal),
                    $"{resolverCase.Name} should reject unsafe runId '{runId}' as runId.");
            }
        }
    }
}
