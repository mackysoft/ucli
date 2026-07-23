using System.Runtime.InteropServices;
using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileReadIndexGenerationStoreSafetyTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task BeginWrite_WhenStagingContainsForeignAndReparseDirectories_DoesNotDeleteThem ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-generation", "staging-directory-safety");
        using var targetScope = TestDirectories.CreateTempScope("read-index-generation", "staging-directory-target");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var stagingRoot = UcliStoragePathResolver.ResolveReadIndexStagingDirectory(storageRoot, fingerprint);
        Directory.CreateDirectory(stagingRoot.Value);
        var foreignDirectory = ContainedPath.Create(
            stagingRoot,
            RootRelativePath.Parse("foreign")).Target;
        Directory.CreateDirectory(foreignDirectory.Value);
        var ownedDirectory = UcliStoragePathResolver.ResolveReadIndexStagingGenerationDirectory(
            storageRoot,
            fingerprint,
            Guid.NewGuid());
        Directory.CreateDirectory(ownedDirectory.Value);
        var targetFile = targetScope.WriteFile("keep.txt", "keep");
        var reparseDirectory = UcliStoragePathResolver.ResolveReadIndexStagingGenerationDirectory(
            storageRoot,
            fingerprint,
            Guid.NewGuid());
        if (!TestSymbolicLinks.TryCreateDirectory(reparseDirectory.Value, targetScope.FullPath))
        {
            return;
        }

        using (await CreateStore().BeginWriteAsync(storageRoot, fingerprint, CancellationToken.None))
        {
        }

        Assert.False(Directory.Exists(ownedDirectory.Value));
        Assert.True(Directory.Exists(foreignDirectory.Value));
        Assert.True(Directory.Exists(reparseDirectory.Value));
        Assert.Equal("keep", await File.ReadAllTextAsync(targetFile));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task BeginWrite_WhenOwnedStagingContainsReparseEntry_RetainsDirectoryAndTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-generation", "staging-entry-safety");
        using var targetScope = TestDirectories.CreateTempScope("read-index-generation", "staging-entry-target");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var stagingDirectory = UcliStoragePathResolver.ResolveReadIndexStagingGenerationDirectory(
            storageRoot,
            fingerprint,
            Guid.NewGuid());
        Directory.CreateDirectory(stagingDirectory.Value);
        var targetFile = targetScope.WriteFile("keep.txt", "keep");
        var linkedFile = ContainedPath.Create(
            stagingDirectory,
            RootRelativePath.Parse("linked.json")).Target;
        if (!TestSymbolicLinks.TryCreateFile(linkedFile.Value, targetFile))
        {
            return;
        }

        using (await CreateStore().BeginWriteAsync(storageRoot, fingerprint, CancellationToken.None))
        {
        }

        Assert.True(Directory.Exists(stagingDirectory.Value));
        Assert.True(File.Exists(linkedFile.Value));
        Assert.Equal("keep", await File.ReadAllTextAsync(targetFile));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task BeginWrite_WhenCurrentGenerationContainsSpecialFile_RejectsGenerationWithoutOpeningFile ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("read-index-generation", "generation-special-file");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var generationId = FileReadIndexArtifactReaderTestSupport.EnsureCurrentGeneration(storageRoot, fingerprint);
        var fifoPath = ContainedPath.Create(
            UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(storageRoot, fingerprint, generationId),
            RootRelativePath.Parse("artifact.fifo")).Target;
        if (MkFifo(fifoPath.Value, Convert.ToUInt32("600", 8)) != 0)
        {
            return;
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateStore()
            .BeginWriteAsync(storageRoot, fingerprint, CancellationToken.None)
            .AsTask());

        _ = File.GetAttributes(fifoPath.Value);
    }

    private static FileReadIndexGenerationStore CreateStore ()
    {
        return new FileReadIndexGenerationStore(
            new FileReadIndexGenerationPointerStore(),
            TimeProvider.System);
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "mkfifo")]
    private static extern int MkFifo (
        string path,
        uint mode);
}
