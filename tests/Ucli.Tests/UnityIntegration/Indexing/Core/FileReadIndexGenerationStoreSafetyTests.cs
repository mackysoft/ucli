using System.Runtime.InteropServices;
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
        var stagingRoot = UcliStoragePathResolver.ResolveReadIndexStagingDirectory(scope.FullPath, fingerprint);
        Directory.CreateDirectory(stagingRoot);
        var foreignDirectory = Path.Combine(stagingRoot, "foreign");
        Directory.CreateDirectory(foreignDirectory);
        var ownedDirectory = UcliStoragePathResolver.ResolveReadIndexStagingGenerationDirectory(
            scope.FullPath,
            fingerprint,
            Guid.NewGuid());
        Directory.CreateDirectory(ownedDirectory);
        var targetFile = targetScope.WriteFile("keep.txt", "keep");
        var reparseDirectory = UcliStoragePathResolver.ResolveReadIndexStagingGenerationDirectory(
            scope.FullPath,
            fingerprint,
            Guid.NewGuid());
        if (!TestSymbolicLinks.TryCreateDirectory(reparseDirectory, targetScope.FullPath))
        {
            return;
        }

        using (await CreateStore().BeginWriteAsync(scope.FullPath, fingerprint, CancellationToken.None))
        {
        }

        Assert.False(Directory.Exists(ownedDirectory));
        Assert.True(Directory.Exists(foreignDirectory));
        Assert.True(Directory.Exists(reparseDirectory));
        Assert.Equal("keep", await File.ReadAllTextAsync(targetFile));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task BeginWrite_WhenOwnedStagingContainsReparseEntry_RetainsDirectoryAndTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-generation", "staging-entry-safety");
        using var targetScope = TestDirectories.CreateTempScope("read-index-generation", "staging-entry-target");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var stagingDirectory = UcliStoragePathResolver.ResolveReadIndexStagingGenerationDirectory(
            scope.FullPath,
            fingerprint,
            Guid.NewGuid());
        Directory.CreateDirectory(stagingDirectory);
        var targetFile = targetScope.WriteFile("keep.txt", "keep");
        var linkedFile = Path.Combine(stagingDirectory, "linked.json");
        if (!TestSymbolicLinks.TryCreateFile(linkedFile, targetFile))
        {
            return;
        }

        using (await CreateStore().BeginWriteAsync(scope.FullPath, fingerprint, CancellationToken.None))
        {
        }

        Assert.True(Directory.Exists(stagingDirectory));
        Assert.True(File.Exists(linkedFile));
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
        var generationId = FileReadIndexArtifactReaderTestSupport.EnsureCurrentGeneration(scope.FullPath, fingerprint);
        var fifoPath = Path.Combine(
            UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(scope.FullPath, fingerprint, generationId),
            "artifact.fifo");
        if (MkFifo(fifoPath, Convert.ToUInt32("600", 8)) != 0)
        {
            return;
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateStore()
            .BeginWriteAsync(scope.FullPath, fingerprint, CancellationToken.None)
            .AsTask());

        _ = File.GetAttributes(fifoPath);
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
