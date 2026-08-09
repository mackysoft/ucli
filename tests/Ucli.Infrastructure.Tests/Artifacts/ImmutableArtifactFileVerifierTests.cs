using System.Runtime.InteropServices;
using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Artifacts;

namespace MackySoft.Ucli.Infrastructure.Tests.Artifacts;

public sealed class ImmutableArtifactFileVerifierTests
{
    private static readonly ArtifactKind Kind = new("test.artifact");

    private static readonly ArtifactMediaType MediaType = new("application/octet-stream");

    private static readonly DateTimeOffset PublicationTime =
        new(2026, 7, 28, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task VerifyAsync_WhenFinalBytesMutate_RejectsTheReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "verify-mutated");
        var directory = scope.CreateDirectory("artifacts");
        var destinationPath = Path.Combine(directory, "capture.bin");
        var contents = new byte[] { 1, 2, 3 };
        var boundaryRoot = AbsolutePath.Parse(scope.FullPath);
        var artifact = await new ImmutableArtifactFilePublisher(() => PublicationTime).PublishAsync(
            Kind,
            MediaType,
            ContainedPath.Create(boundaryRoot, AbsolutePath.Parse(destinationPath)),
            (stream, cancellationToken) => stream.WriteAsync(contents, cancellationToken),
            static (_, _) => ValueTask.CompletedTask,
            CancellationToken.None);
        await File.WriteAllBytesAsync(destinationPath, new byte[] { 3, 2, 1 }, CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() => ImmutableArtifactFileVerifier.VerifyAsync(
                boundaryRoot,
                artifact,
                CancellationToken.None)
            .AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task VerifyAsync_WhenFinalFileHasAnotherHardLink_RejectsTheReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "verify-hard-link");
        var directory = scope.CreateDirectory("artifacts");
        var destinationPath = Path.Combine(directory, "capture.bin");
        var aliasPath = Path.Combine(directory, "capture-alias.bin");
        var contents = new byte[] { 1, 2, 3 };
        var boundaryRoot = AbsolutePath.Parse(scope.FullPath);
        var artifact = await new ImmutableArtifactFilePublisher(() => PublicationTime).PublishAsync(
            Kind,
            MediaType,
            ContainedPath.Create(boundaryRoot, AbsolutePath.Parse(destinationPath)),
            (stream, cancellationToken) => stream.WriteAsync(contents, cancellationToken),
            static (_, _) => ValueTask.CompletedTask,
            CancellationToken.None);
        CreateHardLink(aliasPath, destinationPath);

        await Assert.ThrowsAsync<IOException>(() => ImmutableArtifactFileVerifier.VerifyAsync(
                boundaryRoot,
                artifact,
                CancellationToken.None)
            .AsTask());

        Assert.Equal(contents, await File.ReadAllBytesAsync(destinationPath, CancellationToken.None));
        Assert.Equal(contents, await File.ReadAllBytesAsync(aliasPath, CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_WithUriOnlyReference_DoesNotFetchTheUri ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "verify-uri-only");
        var artifact = new UriArtifactRef(
            Kind,
            MediaType,
            new ArtifactUri("https://example.invalid/artifact.bin"),
            Sha256Digest.Compute(ReadOnlySpan<byte>.Empty),
            sizeBytes: 0,
            PublicationTime);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ImmutableArtifactFileVerifier.VerifyAsync(
                    AbsolutePath.Parse(scope.FullPath),
                    artifact,
                    CancellationToken.None)
                .AsTask());

        Assert.Contains("URI locators are not fetched", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task VerifyAsync_WithPathAndUriReference_VerifiesTheRepositoryPath ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "verify-path-and-uri");
        var directory = scope.CreateDirectory("artifacts");
        var artifactPath = Path.Combine(directory, "capture.bin");
        var contents = new byte[] { 1, 2, 3 };
        await File.WriteAllBytesAsync(artifactPath, contents, CancellationToken.None);
        var artifact = new PathAndUriArtifactRef(
            Kind,
            MediaType,
            new ArtifactPath("artifacts/capture.bin"),
            new ArtifactUri("https://example.invalid/artifact.bin"),
            Sha256Digest.Compute(contents),
            (ulong)contents.Length,
            PublicationTime);

        await ImmutableArtifactFileVerifier.VerifyAsync(
            AbsolutePath.Parse(scope.FullPath),
            artifact,
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task VerifyAsync_WhenAncestorIsSymbolicLink_RejectsPathThatWouldEscapeBoundary ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "verify-symlink-ancestor");
        var boundaryPath = scope.CreateDirectory("boundary");
        var outsidePath = scope.CreateDirectory("outside");
        var outsideFilePath = Path.Combine(outsidePath, "capture.bin");
        var contents = new byte[] { 1, 2, 3 };
        await File.WriteAllBytesAsync(outsideFilePath, contents, CancellationToken.None);
        var linkPath = Path.Combine(boundaryPath, "linked");
        Directory.CreateSymbolicLink(linkPath, outsidePath);

        var artifact = CreatePathArtifact("linked/capture.bin", contents);

        await Assert.ThrowsAsync<IOException>(() => ImmutableArtifactFileVerifier.VerifyAsync(
                AbsolutePath.Parse(boundaryPath),
                artifact,
                CancellationToken.None)
            .AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task VerifyAsync_WhenPathIsMissing_DoesNotCreateDirectories ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-artifacts",
            "verify-missing");
        var missingDirectoryPath = scope.GetPath("missing");
        var artifact = CreatePathArtifact("missing/capture.bin", new byte[] { 1 });

        await Assert.ThrowsAnyAsync<IOException>(() =>
            ImmutableArtifactFileVerifier.VerifyAsync(
                    AbsolutePath.Parse(scope.FullPath),
                    artifact,
                    CancellationToken.None)
                .AsTask());

        Assert.False(Directory.Exists(missingDirectoryPath));
    }

    private static PathArtifactRef CreatePathArtifact (
        string path,
        ReadOnlySpan<byte> contents)
    {
        return new PathArtifactRef(
            Kind,
            MediaType,
            new ArtifactPath(path),
            Sha256Digest.Compute(contents),
            (ulong)contents.Length,
            PublicationTime);
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
