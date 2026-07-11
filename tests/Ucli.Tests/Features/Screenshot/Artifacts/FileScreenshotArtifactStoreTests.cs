using System.Globalization;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Features.Screenshot.Artifacts.Png;

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

        var result = await store.CommitAsync(CreateCommitRequest(paths), CancellationToken.None);

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
        request = caseName switch
        {
            "returned-path" => request with { ReturnedStagingPath = Path.Combine(scope.FullPath, "outside.rgba") },
            "pixel-format" => request with { PixelFormat = "rgba8Linear" },
            "row-order" => request with { RowOrder = "bottomUp" },
            "row-stride" => request with { RowStrideBytes = request.RowStrideBytes + 1 },
            "reported-size" => request with { SizeBytes = request.SizeBytes - 1 },
            "actual-size" => request,
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown raw contract case."),
        };

        var result = await store.CommitAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenshotErrorCodes.ScreenshotCaptureUnsupported, result.Error!.Code);
        Assert.False(File.Exists(paths.PngPath));
        Assert.False(File.Exists(paths.RawStagingPath));
        Assert.False(Directory.Exists(paths.StagingDirectory));
        Assert.False(Directory.Exists(paths.ArtifactDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CommitAsync_WhenArtifactPathEscapesPreparedLayout_ReturnsInvalidArgumentWithoutWritingOutside ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "path-escape");
        var store = CreateStore();
        var paths = Prepare(store, scope);
        await File.WriteAllBytesAsync(paths.RawStagingPath, CreateTwoByTwoRawBytes(), CancellationToken.None);
        var outsidePath = Path.Combine(scope.FullPath, "outside.png");
        var tamperedPaths = paths with { PngPath = outsidePath };

        var result = await store.CommitAsync(CreateCommitRequest(tamperedPaths), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.False(File.Exists(outsidePath));
        Assert.False(File.Exists(paths.PngPath));

        var discardResult = await store.DiscardAsync(paths, CancellationToken.None);
        Assert.True(discardResult.IsSuccess);
        Assert.False(Directory.Exists(paths.StagingDirectory));
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

        var result = await store.CommitAsync(CreateCommitRequest(paths), CancellationToken.None);

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
        var request = new ScreenshotArtifactCommitRequest(
            paths,
            paths.RawStagingPath,
            Width: width,
            Height: 1,
            ContractLiteralCodec.ToValue(IpcScreenshotPixelFormat.Rgba8Srgb),
            ContractLiteralCodec.ToValue(IpcScreenshotRowOrder.TopDown),
            RowStrideBytes: rawBytes.Length,
            SizeBytes: rawBytes.LongLength);

        var result = await store.CommitAsync(request, CancellationToken.None);

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

        var result = await store.CommitAsync(CreateCommitRequest(paths), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(paths.PngPath));
        Assert.Equal(rawBytes, await File.ReadAllBytesAsync(targetRawPath, CancellationToken.None));
        Assert.False(Directory.Exists(paths.StagingDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DiscardAsync_AfterCaptureFailure_RemovesStagingAndDoesNotCreateFinalArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("screenshot-artifact-store", "discard");
        var store = CreateStore();
        var paths = Prepare(store, scope);
        await File.WriteAllBytesAsync(paths.RawStagingPath, CreateTwoByTwoRawBytes(), CancellationToken.None);

        var result = await store.DiscardAsync(paths, CancellationToken.None);

        Assert.True(result.IsSuccess);
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
            store.CommitAsync(CreateCommitRequest(paths), cancellationTokenSource.Token).AsTask());

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
        var paths = Prepare(CreateStore(), scope);
        await File.WriteAllBytesAsync(paths.RawStagingPath, CreateTwoByTwoRawBytes(), CancellationToken.None);
        var deletionAttempts = new List<string>();
        var store = CreateStore(
            new ThrowingTimeProvider(new InvalidOperationException("Expected timestamp failure.")),
            path =>
            {
                deletionAttempts.Add(path);
                if (string.Equals(path, paths.PngPath, StringComparison.Ordinal))
                {
                    throw new IOException("Expected final PNG deletion failure.");
                }

                File.Delete(path);
            });

        var result = await store.CommitAsync(CreateCommitRequest(paths), CancellationToken.None);

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
        var paths = Prepare(CreateStore(), scope);
        await File.WriteAllBytesAsync(paths.RawStagingPath, CreateTwoByTwoRawBytes(), CancellationToken.None);
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

        var result = await store.CommitAsync(CreateCommitRequest(paths), CancellationToken.None);

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
        var paths = Prepare(CreateStore(), scope);
        await File.WriteAllBytesAsync(paths.RawStagingPath, CreateTwoByTwoRawBytes(), CancellationToken.None);
        var store = CreateStore(new ThrowingTimeProvider(
            new OperationCanceledException("Expected late cancellation.")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.CommitAsync(CreateCommitRequest(paths), CancellationToken.None).AsTask());

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
            File.Delete);
    }

    private static FileScreenshotArtifactStore CreateStore (
        TimeProvider timeProvider,
        Action<string>? deleteOwnedFile = null)
    {
        return new FileScreenshotArtifactStore(
            new Rgba8SrgbPngEncoder(),
            new Rgba8SrgbPngValidator(),
            timeProvider,
            deleteOwnedFile ?? File.Delete);
    }

    private static ScreenshotArtifactPaths Prepare (
        FileScreenshotArtifactStore store,
        TestDirectoryScope scope)
    {
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var result = store.Prepare(project, CaptureId);

        Assert.True(result.IsSuccess);
        var paths = Assert.IsType<ScreenshotArtifactPaths>(result.Paths);
        Assert.True(Directory.Exists(paths.StagingDirectory));
        Assert.False(Directory.Exists(paths.ArtifactDirectory));
        return paths;
    }

    private static ScreenshotArtifactCommitRequest CreateCommitRequest (ScreenshotArtifactPaths paths)
    {
        return new ScreenshotArtifactCommitRequest(
            paths,
            paths.RawStagingPath,
            Width: 2,
            Height: 2,
            ContractLiteralCodec.ToValue(IpcScreenshotPixelFormat.Rgba8Srgb),
            ContractLiteralCodec.ToValue(IpcScreenshotRowOrder.TopDown),
            RowStrideBytes: 8,
            SizeBytes: 16);
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
}
