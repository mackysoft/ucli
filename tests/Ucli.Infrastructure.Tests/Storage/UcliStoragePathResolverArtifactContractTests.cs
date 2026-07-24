using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class UcliStoragePathResolverArtifactContractTests
{
    private static readonly Guid ScreenshotCaptureId =
        Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    private static readonly Guid LaunchAttemptId =
        Guid.Parse("11234567-89ab-cdef-0123-456789abcdef");

    private static readonly Guid BootstrapId =
        Guid.Parse("21234567-89ab-cdef-0123-456789abcdef");

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
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveArtifactsDirectory_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveArtifactsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.ArtifactsDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTestArtifactsDirectory_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveTestArtifactsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
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

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.ArtifactsDirectoryName,
            UcliStoragePathNames.TestArtifactsDirectoryName,
            UcliStoragePathResolverTestSupport.RunIdSegment);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveCompileArtifactsDirectory_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveCompileArtifactsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
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

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.ArtifactsDirectoryName,
            UcliStoragePathNames.CompileArtifactsDirectoryName,
            UcliStoragePathResolverTestSupport.RunIdSegment);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveBuildRunDirectory_ReturnsGlobalRunScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveBuildRunDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.RunId);

        UcliStoragePathResolverTestSupport.AssertStoragePath(
            resolvedPath,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.LocalDirectoryName,
            UcliStoragePathNames.BuildRunsDirectoryName,
            UcliStoragePathResolverTestSupport.RunIdSegment);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveBuildRunArtifactsDirectory_ReturnsRunScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.RunId);

        UcliStoragePathResolverTestSupport.AssertStoragePath(
            resolvedPath,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.LocalDirectoryName,
            UcliStoragePathNames.BuildRunsDirectoryName,
            UcliStoragePathResolverTestSupport.RunIdSegment,
            UcliStoragePathNames.ArtifactsDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveBuildRunOutputDirectory_ReturnsRunScopedWorkPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveBuildRunOutputDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.RunId);

        UcliStoragePathResolverTestSupport.AssertStoragePath(
            resolvedPath,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.LocalDirectoryName,
            UcliStoragePathNames.BuildRunsDirectoryName,
            UcliStoragePathResolverTestSupport.RunIdSegment,
            UcliStoragePathNames.WorkDirectoryName,
            UcliStoragePathNames.BuildOutputDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveScreenshotCapturePaths_ReturnCaptureScopedArtifactAndStagingFiles ()
    {
        const string captureIdPathSegment = "04hkaps9lf6uu0938ljojaudts";

        var artifactPath = UcliStoragePathResolver.ResolveScreenshotCaptureArtifactPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            ScreenshotCaptureId);
        var stagingPath = UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            ScreenshotCaptureId);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            artifactPath,
            UcliStoragePathNames.ArtifactsDirectoryName,
            UcliStoragePathNames.ScreenshotDirectoryName,
            captureIdPathSegment,
            UcliStoragePathNames.ScreenshotPngFileName);
        UcliStoragePathResolverTestSupport.AssertProjectPath(
            stagingPath,
            UcliStoragePathNames.WorkDirectoryName,
            UcliStoragePathNames.ScreenshotDirectoryName,
            captureIdPathSegment,
            UcliStoragePathNames.ScreenshotRawStagingFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveOneshotBootstrapPath_ReturnsBootstrapScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            BootstrapId);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.OneshotBootstrapDirectoryName,
            "44hkaps9lf6uu0938ljojaudts" + UcliStoragePathNames.OneshotBootstrapFileExtension);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveOneshotBootstrapPath_WithEmptyBootstrapId_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            UcliStoragePathResolver.ResolveOneshotBootstrapPath(
                UcliStoragePathResolverTestSupport.StorageRoot,
                UcliStoragePathResolverTestSupport.ProjectFingerprint,
                Guid.Empty));

        Assert.Equal("bootstrapId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ScreenshotCapturePathResolvers_WithEmptyCaptureId_ThrowArgumentException ()
    {
        Func<Guid, AbsolutePath>[] resolvers =
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
    public void ResolveLaunchAttemptPaths_ReturnLaunchAttemptScopedPaths ()
    {
        var directoryPath = UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            LaunchAttemptId);
        var diagnosisPath = UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            LaunchAttemptId);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            directoryPath,
            UcliStoragePathNames.LaunchAttemptsDirectoryName,
            "24hkaps9lf6uu0938ljojaudts");
        UcliStoragePathResolverTestSupport.AssertProjectPath(
            diagnosisPath,
            UcliStoragePathNames.LaunchAttemptsDirectoryName,
            "24hkaps9lf6uu0938ljojaudts",
            UcliStoragePathNames.StartupDiagnosisFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void LaunchAttemptPathResolvers_WithEmptyLaunchAttemptId_ThrowArgumentException ()
    {
        Func<Guid, AbsolutePath>[] resolvers =
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
    public void RunScopedPathResolvers_WithEmptyRunId_ThrowArgumentException ()
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

    [Fact]
    [Trait("Size", "Small")]
    public void BuildRunPathResolvers_WithEmptyRunId_ThrowArgumentException ()
    {
        Func<Guid, AbsolutePath>[] resolvers =
        [
            static runId => UcliStoragePathResolver.ResolveBuildRunDirectory(
                UcliStoragePathResolverTestSupport.StorageRoot,
                runId),
            static runId => UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
                UcliStoragePathResolverTestSupport.StorageRoot,
                runId),
            static runId => UcliStoragePathResolver.ResolveBuildRunOutputDirectory(
                UcliStoragePathResolverTestSupport.StorageRoot,
                runId),
        ];

        foreach (var resolve in resolvers)
        {
            var exception = Assert.Throws<ArgumentException>(() => resolve(Guid.Empty));

            Assert.Equal("runId", exception.ParamName);
        }
    }
}
