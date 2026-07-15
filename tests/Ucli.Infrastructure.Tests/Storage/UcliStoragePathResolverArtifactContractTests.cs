using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class UcliStoragePathResolverArtifactContractTests
{
    private static readonly Guid ScreenshotCaptureId =
        Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    private static readonly Guid LaunchAttemptId =
        Guid.Parse("11234567-89ab-cdef-0123-456789abcdef");

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
            UcliStoragePathResolverTestSupport.RunIdText);
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
            UcliStoragePathResolverTestSupport.RunIdText);
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
            UcliStoragePathResolverTestSupport.RunIdText);
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
            UcliStoragePathResolverTestSupport.RunIdText,
            UcliStoragePathNames.BuildOutputDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveScreenshotCapturePaths_ReturnCaptureScopedArtifactAndStagingFiles ()
    {
        var captureIdPathSegment = ScreenshotCaptureId.ToString("N");

        var artifactPath = UcliStoragePathResolver.ResolveScreenshotCaptureArtifactPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            ScreenshotCaptureId);
        var stagingPath = UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            ScreenshotCaptureId);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            artifactPath,
            UcliStoragePathNames.ArtifactsDirectoryName,
            UcliStoragePathNames.ScreenshotDirectoryName,
            captureIdPathSegment,
            UcliStoragePathNames.ScreenshotPngFileName);
        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            stagingPath,
            UcliStoragePathNames.WorkDirectoryName,
            UcliStoragePathNames.ScreenshotDirectoryName,
            captureIdPathSegment,
            UcliStoragePathNames.ScreenshotRawStagingFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ScreenshotCapturePathResolvers_WithEmptyCaptureId_ThrowArgumentException ()
    {
        Func<Guid, string>[] resolvers =
        [
            static captureId => UcliStoragePathResolver.ResolveScreenshotCaptureArtifactsDirectory(
                UcliStoragePathResolverTestSupport.StorageRoot,
                UcliStoragePathResolverTestSupport.ProjectFingerprint,
                captureId),
            static captureId => UcliStoragePathResolver.ResolveScreenshotCaptureArtifactPath(
                UcliStoragePathResolverTestSupport.StorageRoot,
                UcliStoragePathResolverTestSupport.ProjectFingerprint,
                captureId),
            static captureId => UcliStoragePathResolver.ResolveScreenshotCaptureStagingDirectory(
                UcliStoragePathResolverTestSupport.StorageRoot,
                UcliStoragePathResolverTestSupport.ProjectFingerprint,
                captureId),
            static captureId => UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
                UcliStoragePathResolverTestSupport.StorageRoot,
                UcliStoragePathResolverTestSupport.ProjectFingerprint,
                captureId),
        ];

        foreach (var resolve in resolvers)
        {
            var exception = Assert.Throws<ArgumentException>(() => resolve(Guid.Empty));

            Assert.Equal("captureId", exception.ParamName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveLaunchAttemptPaths_ReturnLaunchAttemptScopedPathsWithNFormatIdentifier ()
    {
        var directoryPath = UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            LaunchAttemptId);
        var diagnosisPath = UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            LaunchAttemptId);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            directoryPath,
            UcliStoragePathNames.LaunchAttemptsDirectoryName,
            LaunchAttemptId.ToString("N"));
        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            diagnosisPath,
            UcliStoragePathNames.LaunchAttemptsDirectoryName,
            LaunchAttemptId.ToString("N"),
            UcliStoragePathNames.StartupDiagnosisFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void LaunchAttemptPathResolvers_WithEmptyLaunchAttemptId_ThrowArgumentException ()
    {
        Func<Guid, string>[] resolvers =
        [
            static launchAttemptId => UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
                UcliStoragePathResolverTestSupport.StorageRoot,
                UcliStoragePathResolverTestSupport.ProjectFingerprint,
                launchAttemptId),
            static launchAttemptId => UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
                UcliStoragePathResolverTestSupport.StorageRoot,
                UcliStoragePathResolverTestSupport.ProjectFingerprint,
                launchAttemptId),
        ];

        foreach (var resolve in resolvers)
        {
            var exception = Assert.Throws<ArgumentException>(() => resolve(Guid.Empty));

            Assert.Equal("launchAttemptId", exception.ParamName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RunScopedPathResolvers_WithPathSegmentOrTraversalRunId_ThrowArgumentException ()
    {
        foreach (var resolverCase in RunScopedPathResolvers)
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                resolverCase.Resolve(
                    UcliStoragePathResolverTestSupport.StorageRoot,
                    UcliStoragePathResolverTestSupport.ProjectFingerprint,
                    Guid.Empty));

            Assert.True(
                string.Equals("runId", exception.ParamName, StringComparison.Ordinal),
                $"{resolverCase.Name} should reject an empty runId as runId.");
        }
    }
}
