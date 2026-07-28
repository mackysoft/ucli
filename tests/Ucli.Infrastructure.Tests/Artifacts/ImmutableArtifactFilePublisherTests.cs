using System.Runtime.InteropServices;
using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Artifacts;

namespace MackySoft.Ucli.Infrastructure.Tests.Artifacts;

public sealed class ImmutableArtifactFilePublisherTests
{
    private static readonly ArtifactKind Kind = new("test.artifact");

    private static readonly ArtifactMediaType MediaType = new("application/octet-stream");

    private static readonly DateTimeOffset PublicationTime =
        new(2026, 7, 28, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishAsync_WithNewDestination_ReturnsReferenceMeasuredFromReopenedFinalBytes ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "publish-success");
        var directory = scope.CreateDirectory("artifacts");
        var destinationPath = Path.Combine(directory, "capture.bin");
        var contents = new byte[] { 0x00, 0x7f, 0x80, 0xff };
        var boundaryRoot = AbsolutePath.Parse(scope.FullPath);
        var publisher = new ImmutableArtifactFilePublisher(() => PublicationTime);
        Stream? retainedWriter = null;

        var artifact = await publisher.PublishAsync(
            Kind,
            MediaType,
            ContainedPath.Create(boundaryRoot, AbsolutePath.Parse(destinationPath)),
            async (destination, cancellationToken) =>
            {
                Assert.False(destination.CanRead);
                Assert.True(destination.CanWrite);
                retainedWriter = destination;
                await destination.WriteAsync(contents, cancellationToken);
                destination.Dispose();
            },
            async (source, cancellationToken) =>
            {
                Assert.True(source.CanRead);
                Assert.False(source.CanWrite);
                Assert.Throws<NotSupportedException>(() => source.WriteByte(0xff));
                using var validatedBytes = new MemoryStream();
                await source.CopyToAsync(validatedBytes, cancellationToken);
                Assert.Equal(contents, validatedBytes.ToArray());
                source.Dispose();
            },
            CancellationToken.None);

        var writer = Assert.IsAssignableFrom<Stream>(retainedWriter);
        Assert.Throws<ObjectDisposedException>(() => writer.WriteByte(0xff));
        Assert.Equal(Kind, artifact.Kind);
        Assert.Equal(MediaType, artifact.MediaType);
        Assert.Equal("artifacts/capture.bin", artifact.Path.Value);
        Assert.Equal(Sha256Digest.Compute(contents), artifact.Digest);
        Assert.Equal((ulong)contents.Length, artifact.SizeBytes);
        Assert.Equal(PublicationTime, artifact.CreatedAtUtc);
        Assert.Empty(EnumeratePrivateCandidatePaths(directory));
        Assert.Equal(contents, await File.ReadAllBytesAsync(destinationPath, CancellationToken.None));

        await ImmutableArtifactFileVerifier.VerifyAsync(
            boundaryRoot,
            artifact,
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishAsync_WhenDestinationExists_DoesNotReplaceOrDeleteExistingBytes ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "publish-no-overwrite");
        var directory = scope.CreateDirectory("artifacts");
        var destinationPath = Path.Combine(directory, "capture.bin");
        var temporaryContents = new byte[] { 1, 2, 3 };
        var existingContents = new byte[] { 9, 8, 7 };
        await File.WriteAllBytesAsync(destinationPath, existingContents, CancellationToken.None);
        var boundaryRoot = AbsolutePath.Parse(scope.FullPath);
        var publisher = new ImmutableArtifactFilePublisher(() => PublicationTime);

        await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync(
                Kind,
                MediaType,
                ContainedPath.Create(boundaryRoot, AbsolutePath.Parse(destinationPath)),
                CreateWriteCallback(temporaryContents),
                ValidateSourceAsync,
                CancellationToken.None)
            .AsTask());

        Assert.Equal(existingContents, await File.ReadAllBytesAsync(destinationPath, CancellationToken.None));
        Assert.Equal(
            temporaryContents,
            await File.ReadAllBytesAsync(
                GetSinglePrivateCandidatePath(directory),
                CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishAsync_WhenValidationFails_LeavesPrivateCandidateWithoutPublishingDestination ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "publish-validation-failure");
        var directory = scope.CreateDirectory("artifacts");
        var destinationPath = Path.Combine(directory, "capture.bin");
        var temporaryContents = new byte[] { 1, 2, 3 };
        var boundaryRoot = AbsolutePath.Parse(scope.FullPath);
        var publisher = new ImmutableArtifactFilePublisher(() => PublicationTime);

        await Assert.ThrowsAsync<InvalidDataException>(() => publisher.PublishAsync(
                Kind,
                MediaType,
                ContainedPath.Create(boundaryRoot, AbsolutePath.Parse(destinationPath)),
                CreateWriteCallback(temporaryContents),
                static (_, _) => ValueTask.FromException(
                    new InvalidDataException("Feature validation failed.")),
                CancellationToken.None)
            .AsTask());

        Assert.False(File.Exists(destinationPath));
        Assert.Equal(
            temporaryContents,
            await File.ReadAllBytesAsync(
                GetSinglePrivateCandidatePath(directory),
                CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishAsync_WhenPostPublicationStepFails_LeavesMovedBytesWithoutReturningReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "publish-post-move-failure");
        var directory = scope.CreateDirectory("artifacts");
        var destinationPath = Path.Combine(directory, "capture.bin");
        var contents = new byte[] { 1, 2, 3 };
        var boundaryRoot = AbsolutePath.Parse(scope.FullPath);
        var publisher = new ImmutableArtifactFilePublisher(
            () => throw new InvalidOperationException("UTC clock failed."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync(
                Kind,
                MediaType,
                ContainedPath.Create(boundaryRoot, AbsolutePath.Parse(destinationPath)),
                CreateWriteCallback(contents),
                ValidateSourceAsync,
                CancellationToken.None)
            .AsTask());

        Assert.Empty(EnumeratePrivateCandidatePaths(directory));
        Assert.Equal(contents, await File.ReadAllBytesAsync(destinationPath, CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishAsync_WhenValidationReplacesPrivateCandidate_RejectsAndDoesNotDeleteReplacement ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "publish-source-replaced");
        var directory = scope.CreateDirectory("artifacts");
        var movedSourcePath = Path.Combine(directory, "moved-source");
        var destinationPath = Path.Combine(directory, "capture.bin");
        var sourceContents = new byte[] { 1, 2, 3 };
        var replacementContents = new byte[] { 9, 8, 7 };
        var boundaryRoot = AbsolutePath.Parse(scope.FullPath);
        var publisher = new ImmutableArtifactFilePublisher(() => PublicationTime);

        await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync(
                Kind,
                MediaType,
                ContainedPath.Create(boundaryRoot, AbsolutePath.Parse(destinationPath)),
                CreateWriteCallback(sourceContents),
                async (source, cancellationToken) =>
                {
                    var candidatePath = GetSinglePrivateCandidatePath(directory);
                    File.Move(candidatePath, movedSourcePath);
                    await File.WriteAllBytesAsync(
                        candidatePath,
                        replacementContents,
                        cancellationToken);
                    using var validatedBytes = new MemoryStream();
                    await source.CopyToAsync(validatedBytes, cancellationToken);
                    Assert.Equal(sourceContents, validatedBytes.ToArray());
                },
                CancellationToken.None)
            .AsTask());

        Assert.Equal(
            replacementContents,
            await File.ReadAllBytesAsync(
                GetSinglePrivateCandidatePath(directory),
                CancellationToken.None));
        Assert.Equal(
            sourceContents,
            await File.ReadAllBytesAsync(movedSourcePath, CancellationToken.None));
        Assert.False(File.Exists(destinationPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishAsync_WhenPrivateCandidateHasAnotherHardLink_RejectsReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "publish-hard-link");
        var directory = scope.CreateDirectory("artifacts");
        var aliasPath = Path.Combine(directory, "capture-alias.bin");
        var destinationPath = Path.Combine(directory, "capture.bin");
        var contents = new byte[] { 1, 2, 3 };
        var boundaryRoot = AbsolutePath.Parse(scope.FullPath);
        var publisher = new ImmutableArtifactFilePublisher(() => PublicationTime);

        await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync(
                Kind,
                MediaType,
                ContainedPath.Create(boundaryRoot, AbsolutePath.Parse(destinationPath)),
                CreateWriteCallback(contents),
                (_, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CreateHardLink(aliasPath, GetSinglePrivateCandidatePath(directory));
                    return ValueTask.CompletedTask;
                },
                CancellationToken.None)
            .AsTask());

        Assert.Equal(
            contents,
            await File.ReadAllBytesAsync(
                GetSinglePrivateCandidatePath(directory),
                CancellationToken.None));
        Assert.Equal(contents, await File.ReadAllBytesAsync(aliasPath, CancellationToken.None));
        Assert.False(File.Exists(destinationPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishAsync_WhenDestinationIsReplacedAfterFinalRead_RejectsAndDoesNotDeleteReplacement ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "publish-destination-replaced");
        var directory = scope.CreateDirectory("artifacts");
        var destinationPath = Path.Combine(directory, "capture.bin");
        var movedDestinationPath = Path.Combine(directory, "moved-destination");
        var sourceContents = new byte[] { 1, 2, 3 };
        var replacementContents = new byte[] { 9, 8, 7 };
        var boundaryRoot = AbsolutePath.Parse(scope.FullPath);
        var publisher = new ImmutableArtifactFilePublisher(
            () =>
            {
                File.Move(destinationPath, movedDestinationPath);
                File.WriteAllBytes(destinationPath, replacementContents);
                return PublicationTime;
            });

        await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync(
                Kind,
                MediaType,
                ContainedPath.Create(boundaryRoot, AbsolutePath.Parse(destinationPath)),
                CreateWriteCallback(sourceContents),
                ValidateSourceAsync,
                CancellationToken.None)
            .AsTask());

        Assert.Empty(EnumeratePrivateCandidatePaths(directory));
        Assert.Equal(
            replacementContents,
            await File.ReadAllBytesAsync(destinationPath, CancellationToken.None));
        Assert.Equal(
            sourceContents,
            await File.ReadAllBytesAsync(movedDestinationPath, CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishAsync_WhenFinalWriteIsAttemptedDuringPublicationTimeCapture_DoesNotReturnAChangedReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "publish-final-bytes-changed");
        var directory = scope.CreateDirectory("artifacts");
        var destinationPath = Path.Combine(directory, "capture.bin");
        var sourceContents = new byte[] { 1, 2, 3 };
        var changedContents = new byte[] { 9, 8, 7 };
        var boundaryRoot = AbsolutePath.Parse(scope.FullPath);
        var writeAttempted = false;
        var writeCompleted = false;
        var publisher = new ImmutableArtifactFilePublisher(
            () =>
            {
                writeAttempted = true;
                File.WriteAllBytes(destinationPath, changedContents);
                writeCompleted = true;
                return PublicationTime;
            });

        await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync(
                Kind,
                MediaType,
                ContainedPath.Create(boundaryRoot, AbsolutePath.Parse(destinationPath)),
                CreateWriteCallback(sourceContents),
                ValidateSourceAsync,
                CancellationToken.None)
            .AsTask());

        Assert.Empty(EnumeratePrivateCandidatePaths(directory));
        Assert.True(writeAttempted);
        Assert.Equal(
            writeCompleted ? changedContents : sourceContents,
            await File.ReadAllBytesAsync(destinationPath, CancellationToken.None));
        Assert.Equal(!OperatingSystem.IsWindows(), writeCompleted);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishAsync_WhenDestinationAncestorReplacementIsAttemptedAfterFinalRead_DoesNotReturnAReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "publish-ancestor-replaced");
        var directory = scope.CreateDirectory("artifacts");
        var movedDirectory = scope.GetPath("moved-artifacts");
        var destinationPath = Path.Combine(directory, "capture.bin");
        var movedDestinationPath = Path.Combine(movedDirectory, "capture.bin");
        var sourceContents = new byte[] { 1, 2, 3 };
        var replacementContents = new byte[] { 9, 8, 7 };
        var boundaryRoot = AbsolutePath.Parse(scope.FullPath);
        var replacementAttempted = false;
        var replacementCompleted = false;
        var publisher = new ImmutableArtifactFilePublisher(
            () =>
            {
                replacementAttempted = true;
                Directory.Move(directory, movedDirectory);
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(destinationPath, replacementContents);
                replacementCompleted = true;
                return PublicationTime;
            });

        await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync(
                Kind,
                MediaType,
                ContainedPath.Create(boundaryRoot, AbsolutePath.Parse(destinationPath)),
                CreateWriteCallback(sourceContents),
                ValidateSourceAsync,
                CancellationToken.None)
            .AsTask());

        Assert.Empty(EnumeratePrivateCandidatePaths(directory));
        Assert.True(replacementAttempted);
        if (replacementCompleted)
        {
            Assert.Equal(
                replacementContents,
                await File.ReadAllBytesAsync(destinationPath, CancellationToken.None));
            Assert.Equal(
                sourceContents,
                await File.ReadAllBytesAsync(movedDestinationPath, CancellationToken.None));
        }
        else
        {
            Assert.True(OperatingSystem.IsWindows());
            Assert.Equal(
                sourceContents,
                await File.ReadAllBytesAsync(destinationPath, CancellationToken.None));
            Assert.False(File.Exists(movedDestinationPath));
        }
    }

    private static Func<Stream, CancellationToken, ValueTask> CreateWriteCallback (
        ReadOnlyMemory<byte> contents)
    {
        return (destination, cancellationToken) =>
            destination.WriteAsync(contents, cancellationToken);
    }

    private static ValueTask ValidateSourceAsync (
        Stream source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.True(source.CanRead);
        return ValueTask.CompletedTask;
    }

    private static string GetSinglePrivateCandidatePath (string directory)
    {
        return Assert.Single(EnumeratePrivateCandidatePaths(directory));
    }

    private static IEnumerable<string> EnumeratePrivateCandidatePaths (string directory)
    {
        return Directory.EnumerateFiles(
            directory,
            ".tmp-*",
            SearchOption.TopDirectoryOnly);
    }

    private static void CreateHardLink (
        string linkPath,
        string existingPath)
    {
        var result = OperatingSystem.IsWindows()
            ? CreateHardLinkWindows(linkPath, existingPath, IntPtr.Zero) ? 0 : -1
            : CreateHardLinkPosix(existingPath, linkPath);
        if (result != 0)
        {
            throw new IOException(
                $"Test hard link could not be created. error={Marshal.GetLastWin32Error()}");
        }
    }

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        EntryPoint = "CreateHardLinkW",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkWindows (
        string fileName,
        string existingFileName,
        IntPtr securityAttributes);

    [DllImport("libc", SetLastError = true, EntryPoint = "link")]
    private static extern int CreateHardLinkPosix (
        string existingPath,
        string linkPath);
}
