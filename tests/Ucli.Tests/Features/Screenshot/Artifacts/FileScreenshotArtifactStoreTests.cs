using System.Globalization;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Features.Screenshot.Artifacts.Png;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Features.Screenshot.Artifacts;

public sealed class FileScreenshotArtifactStoreTests
{
    private const string CaptureId = "20260711_120000Z_deadbeef";
    private static readonly DateTimeOffset CreatedAtUtc = DateTimeOffset.Parse(
        "2026-07-11T12:00:00Z",
        CultureInfo.InvariantCulture);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CommitAsync_WithNormalizedRawImage_AtomicallyCommitsValidatedPngAndCleansStaging ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "commit-success");
        var store = CreateStore();
        var paths = Prepare(store, scope);
        var rawBytes = CreateTwoByTwoRawBytes();
        await File.WriteAllBytesAsync(paths.RawStagingPath, rawBytes, CancellationToken.None);

        var result = await paths.Lease.CommitAsync(CreateCommitRequest(paths), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var artifact = Assert.IsType<ScreenshotArtifact>(result.Artifact);
        Assert.Equal(CreatedAtUtc, artifact.CreatedAtUtc);
        Assert.False(Path.IsPathRooted(artifact.Path));
        Assert.DoesNotContain('\\', artifact.Path);
        Assert.EndsWith("/artifacts/screenshot/20260711_120000Z_deadbeef/screenshot.png", artifact.Path, StringComparison.Ordinal);
        Assert.True(File.Exists(paths.PngPath));
        Assert.False(File.Exists(paths.RawStagingPath));
        Assert.False(Directory.Exists(paths.StagingDirectory));
        Assert.Single(Directory.EnumerateFiles(paths.ArtifactDirectory));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(paths.ArtifactDirectory),
            static path => path.Contains(".tmp.", StringComparison.Ordinal));

        var pngBytes = await File.ReadAllBytesAsync(paths.PngPath, CancellationToken.None);
        Assert.Equal(pngBytes.LongLength, artifact.SizeBytes);
        Assert.Equal(Sha256LowerHex.Compute(pngBytes), artifact.Digest);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, pngBytes[..8]);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Discard_AfterSuccessfulCommit_IsIdempotentAndPreservesCommittedArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "discard-committed");
        var paths = Prepare(CreateStore(), scope);
        await File.WriteAllBytesAsync(paths.RawStagingPath, CreateTwoByTwoRawBytes(), CancellationToken.None);
        var commitResult = await paths.Lease.CommitAsync(CreateCommitRequest(paths), CancellationToken.None);
        Assert.True(commitResult.IsSuccess);

        var firstDiscard = paths.Lease.Discard();
        var secondDiscard = paths.Lease.Discard();

        Assert.True(firstDiscard.IsSuccess);
        Assert.True(secondDiscard.IsSuccess);
        Assert.True(File.Exists(paths.PngPath));
        Assert.False(File.Exists(paths.RawStagingPath));
        Assert.False(Directory.Exists(paths.StagingDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Prepare_WhenSecureDirectoryCreationFailsAfterCreatingCaptureDirectory_RollsBackCaptureDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "prepare-rollback");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var expectedPaths = ResolveExpectedPaths(project);
        var store = CreateStore(
            new ManualTimeProvider(CreatedAtUtc),
            ensureSecureStagingDirectory: path =>
            {
                Directory.CreateDirectory(path);
                throw new IOException("Expected secure staging failure.");
            });

        var result = store.Prepare(project, CaptureId);

        Assert.False(result.IsSuccess);
        Assert.Contains("Expected secure staging failure.", result.Error!.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("rollback also failed", result.Error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(expectedPaths.StagingDirectory));
        Assert.False(Directory.Exists(expectedPaths.ArtifactDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Prepare_WhenSecureDirectoryCreationAndRollbackFail_ReturnsBothDiagnostics ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "prepare-rollback-failure");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var expectedPaths = ResolveExpectedPaths(project);
        var unexpectedPath = Path.Combine(expectedPaths.StagingDirectory, "unexpected.txt");
        var store = CreateStore(
            new ManualTimeProvider(CreatedAtUtc),
            ensureSecureStagingDirectory: path =>
            {
                Directory.CreateDirectory(path);
                File.WriteAllText(unexpectedPath, "unexpected");
                throw new IOException("Expected secure staging failure.");
            });

        var result = store.Prepare(project, CaptureId);

        Assert.False(result.IsSuccess);
        Assert.Contains("Expected secure staging failure.", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("rollback also failed", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("unexpected entries", result.Error.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(unexpectedPath));
        Assert.False(Directory.Exists(expectedPaths.ArtifactDirectory));
    }

    [Theory]
    [InlineData("returned-path")]
    [InlineData("pixel-format")]
    [InlineData("row-order")]
    [InlineData("row-stride")]
    [InlineData("reported-size")]
    [InlineData("actual-size")]
    [Trait("Size", "Medium")]
    public async Task CommitAsync_WithInvalidRawContract_ReturnsCaptureUnsupportedWithoutFinalArtifact (string caseName)
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", $"invalid-{caseName}");
        var store = CreateStore();
        var paths = Prepare(store, scope);
        var rawBytes = CreateTwoByTwoRawBytes();
        if (caseName == "actual-size")
        {
            Array.Resize(ref rawBytes, rawBytes.Length - 1);
        }

        await File.WriteAllBytesAsync(paths.RawStagingPath, rawBytes, CancellationToken.None);
        var request = CreateCommitRequest(paths);
        var staging = request.Staging;
        request = caseName switch
        {
            "returned-path" => CreateCommitRequest(
                paths,
                staging: staging with { Path = Path.Combine(scope.FullPath, "outside.rgba") }),
            "pixel-format" => CreateCommitRequest(
                paths,
                staging: staging with { PixelFormat = (IpcScreenshotPixelFormat)int.MaxValue }),
            "row-order" => CreateCommitRequest(
                paths,
                staging: staging with { RowOrder = (IpcScreenshotRowOrder)int.MaxValue }),
            "row-stride" => CreateCommitRequest(
                paths,
                staging: staging with { RowStrideBytes = staging.RowStrideBytes + 1 }),
            "reported-size" => CreateCommitRequest(
                paths,
                staging: staging with { SizeBytes = staging.SizeBytes - 1 }),
            "actual-size" => request,
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown raw contract case."),
        };

        var result = await paths.Lease.CommitAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenshotErrorCodes.ScreenshotCaptureUnsupported, result.Error!.Code);
        Assert.False(File.Exists(paths.PngPath));
        Assert.False(File.Exists(paths.RawStagingPath));
        Assert.False(Directory.Exists(paths.StagingDirectory));
        Assert.False(Directory.Exists(paths.ArtifactDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CommitAsync_WhenRawStagingIsSymbolicLink_RejectsLinkWithoutChangingTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "raw-symlink");
        var store = CreateStore();
        var paths = Prepare(store, scope);
        var targetPath = Path.Combine(scope.FullPath, "target.rgba");
        var rawBytes = CreateTwoByTwoRawBytes();
        await File.WriteAllBytesAsync(targetPath, rawBytes, CancellationToken.None);
        try
        {
            File.CreateSymbolicLink(paths.RawStagingPath, targetPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return;
        }

        var result = await paths.Lease.CommitAsync(CreateCommitRequest(paths), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(paths.PngPath));
        Assert.Equal(rawBytes, await File.ReadAllBytesAsync(targetPath, CancellationToken.None));
        Assert.False(File.Exists(paths.RawStagingPath));
        Assert.False(Directory.Exists(paths.StagingDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CommitAsync_WhenDimensionsExceedHostLimit_RejectsBeforeEncodingAndRemovesStaging ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "oversized");
        var store = CreateStore();
        var paths = Prepare(store, scope);
        var width = IpcScreenshotCaptureLimits.MaximumDimension + 1;
        var rawBytes = new byte[checked(width * 4)];
        await File.WriteAllBytesAsync(paths.RawStagingPath, rawBytes, CancellationToken.None);
        var request = CreateCommitRequest(
            paths,
            width,
            height: 1,
            new IpcScreenshotStagingImage(
                paths.RawStagingPath,
                IpcScreenshotPixelFormat.Rgba8Srgb,
                IpcScreenshotRowOrder.TopDown,
                RowStrideBytes: rawBytes.Length,
                SizeBytes: rawBytes.LongLength));

        var result = await paths.Lease.CommitAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenshotErrorCodes.ScreenshotCaptureUnsupported, result.Error!.Code);
        Assert.False(File.Exists(paths.PngPath));
        Assert.False(File.Exists(paths.RawStagingPath));
        Assert.False(Directory.Exists(paths.StagingDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CommitAsync_WhenStagingDirectoryIsSymbolicLink_RejectsAncestorWithoutChangingTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "staging-directory-symlink");
        var store = CreateStore();
        var paths = Prepare(store, scope);
        Directory.Delete(paths.StagingDirectory);
        var targetDirectory = scope.CreateDirectory("outside-staging");
        var targetRawPath = Path.Combine(targetDirectory, "capture.rgba");
        var rawBytes = CreateTwoByTwoRawBytes();
        await File.WriteAllBytesAsync(targetRawPath, rawBytes, CancellationToken.None);
        try
        {
            Directory.CreateSymbolicLink(paths.StagingDirectory, targetDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return;
        }

        var result = await paths.Lease.CommitAsync(CreateCommitRequest(paths), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(paths.PngPath));
        Assert.Equal(rawBytes, await File.ReadAllBytesAsync(targetRawPath, CancellationToken.None));
        Assert.False(Directory.Exists(paths.StagingDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Discard_AfterCaptureFailure_RemovesStagingAndDoesNotCreateFinalArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "discard");
        var store = CreateStore();
        var paths = Prepare(store, scope);
        await File.WriteAllBytesAsync(paths.RawStagingPath, CreateTwoByTwoRawBytes(), CancellationToken.None);

        var firstResult = paths.Lease.Discard();
        var secondResult = paths.Lease.Discard();

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.False(File.Exists(paths.RawStagingPath));
        Assert.False(Directory.Exists(paths.StagingDirectory));
        Assert.False(File.Exists(paths.PngPath));
        Assert.False(Directory.Exists(paths.ArtifactDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CommitAsync_WhenAlreadyCanceled_ThrowsAndStillRemovesPreparedStaging ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "canceled");
        var store = CreateStore();
        var paths = Prepare(store, scope);
        await File.WriteAllBytesAsync(paths.RawStagingPath, CreateTwoByTwoRawBytes(), CancellationToken.None);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            paths.Lease.CommitAsync(CreateCommitRequest(paths), cancellationTokenSource.Token).AsTask());

        Assert.False(File.Exists(paths.RawStagingPath));
        Assert.False(Directory.Exists(paths.StagingDirectory));
        Assert.False(File.Exists(paths.PngPath));
        Assert.False(Directory.Exists(paths.ArtifactDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CommitAsync_WhenFinalArtifactDeletionFails_StillCleansStagingAndReturnsFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "final-delete-failure");
        var expectedPaths = ResolveExpectedPaths(scope);
        var deletionAttempts = new List<string>();
        var store = CreateStore(
            new ThrowingTimeProvider(new InvalidOperationException("Expected timestamp failure.")),
            path =>
            {
                deletionAttempts.Add(path);
                if (string.Equals(path, expectedPaths.PngPath, StringComparison.Ordinal))
                {
                    throw new IOException("Expected final PNG deletion failure.");
                }

                File.Delete(path);
            });
        var paths = Prepare(store, scope);
        await File.WriteAllBytesAsync(paths.RawStagingPath, CreateTwoByTwoRawBytes(), CancellationToken.None);

        var result = await paths.Lease.CommitAsync(CreateCommitRequest(paths), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Expected final PNG deletion failure.", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains(paths.PngPath, deletionAttempts);
        Assert.False(File.Exists(paths.RawStagingPath));
        Assert.False(Directory.Exists(paths.StagingDirectory));
        Assert.True(File.Exists(paths.PngPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CommitAsync_WhenTemporaryArtifactDeletionFails_StillRemovesFinalArtifactAndStaging ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "temporary-delete-failure");
        var deletionAttempts = new List<string>();
        var store = CreateStore(
            new ManualTimeProvider(CreatedAtUtc),
            path =>
            {
                deletionAttempts.Add(path);
                if (path.Contains(".tmp.", StringComparison.Ordinal))
                {
                    throw new IOException("Expected temporary PNG deletion failure.");
                }

                File.Delete(path);
            });
        var paths = Prepare(store, scope);
        await File.WriteAllBytesAsync(paths.RawStagingPath, CreateTwoByTwoRawBytes(), CancellationToken.None);

        var result = await paths.Lease.CommitAsync(CreateCommitRequest(paths), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Expected temporary PNG deletion failure.", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains(paths.PngPath, deletionAttempts);
        Assert.False(File.Exists(paths.RawStagingPath));
        Assert.False(Directory.Exists(paths.StagingDirectory));
        Assert.False(File.Exists(paths.PngPath));
        Assert.False(Directory.Exists(paths.ArtifactDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CommitAsync_WhenCanceledAfterFinalArtifactCreation_RethrowsAndCleansEveryOwnedPath ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "late-cancellation");
        var store = CreateStore(new ThrowingTimeProvider(
            new OperationCanceledException("Expected late cancellation.")));
        var paths = Prepare(store, scope);
        await File.WriteAllBytesAsync(paths.RawStagingPath, CreateTwoByTwoRawBytes(), CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            paths.Lease.CommitAsync(CreateCommitRequest(paths), CancellationToken.None).AsTask());

        Assert.False(File.Exists(paths.RawStagingPath));
        Assert.False(Directory.Exists(paths.StagingDirectory));
        Assert.False(File.Exists(paths.PngPath));
        Assert.False(Directory.Exists(paths.ArtifactDirectory));
    }

    private static FileScreenshotArtifactStore CreateStore ()
    {
        return new FileScreenshotArtifactStore(
            new Rgba8SrgbPngEncoder(),
            new Rgba8SrgbPngValidator(),
            new ManualTimeProvider(CreatedAtUtc),
            FileSystemAccessBoundary.EnsureSecureDirectory,
            File.Delete);
    }

    private static FileScreenshotArtifactStore CreateStore (
        TimeProvider timeProvider,
        Action<string>? deleteOwnedFile = null,
        Action<string>? ensureSecureStagingDirectory = null)
    {
        return new FileScreenshotArtifactStore(
            new Rgba8SrgbPngEncoder(),
            new Rgba8SrgbPngValidator(),
            timeProvider,
            ensureSecureStagingDirectory ?? FileSystemAccessBoundary.EnsureSecureDirectory,
            deleteOwnedFile ?? File.Delete);
    }

    private static PreparedCapture Prepare (
        FileScreenshotArtifactStore store,
        TestDirectoryScope scope)
    {
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var expectedPaths = ResolveExpectedPaths(project);
        var result = store.Prepare(project, CaptureId);

        Assert.True(result.IsSuccess);
        var lease = Assert.IsAssignableFrom<IScreenshotArtifactLease>(result.Lease);
        Assert.Equal(expectedPaths.RawStagingPath, lease.RawStagingPath);
        Assert.True(Path.IsPathRooted(lease.RawStagingPath));
        Assert.True(Directory.Exists(expectedPaths.StagingDirectory));
        Assert.False(Directory.Exists(expectedPaths.ArtifactDirectory));
        return new PreparedCapture(lease, expectedPaths);
    }

    private static ScreenshotArtifactCommitRequest CreateCommitRequest (
        PreparedCapture capture,
        int width = 2,
        int height = 2,
        IpcScreenshotStagingImage? staging = null)
    {
        return new ScreenshotArtifactCommitRequest(
            width,
            height,
            staging ?? new IpcScreenshotStagingImage(
                capture.RawStagingPath,
                IpcScreenshotPixelFormat.Rgba8Srgb,
                IpcScreenshotRowOrder.TopDown,
                RowStrideBytes: checked(width * 4),
                SizeBytes: checked((long)width * height * 4)));
    }

    private static ExpectedCapturePaths ResolveExpectedPaths (TestDirectoryScope scope)
    {
        return ResolveExpectedPaths(
            ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint"));
    }

    private static ExpectedCapturePaths ResolveExpectedPaths (ResolvedUnityProjectContext project)
    {
        return new ExpectedCapturePaths(
            UcliStoragePathResolver.ResolveScreenshotCaptureArtifactsDirectory(
                project.RepositoryRoot,
                project.ProjectFingerprint,
                CaptureId),
            UcliStoragePathResolver.ResolveScreenshotCaptureArtifactPath(
                project.RepositoryRoot,
                project.ProjectFingerprint,
                CaptureId),
            UcliStoragePathResolver.ResolveScreenshotCaptureStagingDirectory(
                project.RepositoryRoot,
                project.ProjectFingerprint,
                CaptureId),
            UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
                project.RepositoryRoot,
                project.ProjectFingerprint,
                CaptureId));
    }

    private static byte[] CreateTwoByTwoRawBytes ()
    {
        return
        [
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
            255, 255, 255, 255,
        ];
    }

    private sealed class ThrowingTimeProvider : TimeProvider
    {
        private readonly Exception exception;

        public ThrowingTimeProvider (Exception exception)
        {
            this.exception = exception;
        }

        public override DateTimeOffset GetUtcNow ()
        {
            throw exception;
        }
    }

    private sealed record PreparedCapture (
        IScreenshotArtifactLease Lease,
        ExpectedCapturePaths Paths)
    {
        public string ArtifactDirectory => Paths.ArtifactDirectory;

        public string PngPath => Paths.PngPath;

        public string StagingDirectory => Paths.StagingDirectory;

        public string RawStagingPath => Lease.RawStagingPath;
    }

    private sealed record ExpectedCapturePaths (
        string ArtifactDirectory,
        string PngPath,
        string StagingDirectory,
        string RawStagingPath);
}
