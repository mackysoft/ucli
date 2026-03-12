using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests.Storage;

public sealed class FileUtilitiesTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAllTextAtomically_WhenTargetExists_ReplacesExistingContents ()
    {
        using var scope = TestDirectories.CreateTempScope("contracts-storage", "atomic-write-overwrite");
        var path = Path.Combine(scope.FullPath, "daemon-diagnosis.json");
        await File.WriteAllTextAsync(path, "old-contents", CancellationToken.None);

        await FileUtilities.WriteAllTextAtomically(path, "new-contents", CancellationToken.None);

        var contents = await File.ReadAllTextAsync(path, CancellationToken.None);
        Assert.Equal("new-contents", contents);

        var files = Directory.GetFiles(scope.FullPath);
        Assert.Single(files);
        Assert.Equal(Path.GetFullPath(path), Path.GetFullPath(files[0]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAllTextAtomically_WhenTargetIsUnderUcliLocal_BootstrapsSharedStorage ()
    {
        using var scope = TestDirectories.CreateTempScope("contracts-storage", "atomic-write-bootstrap");
        var storageRoot = scope.CreateDirectory("Repo");
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
        var ucliDirectoryPath = Path.Combine(storageRoot, UcliStoragePathNames.UcliDirectoryName);
        var localDirectoryPath = Path.Combine(ucliDirectoryPath, UcliStoragePathNames.LocalDirectoryName);
        var gitIgnorePath = Path.Combine(ucliDirectoryPath, UcliStoragePathNames.GitIgnoreFileName);

        await FileUtilities.WriteAllTextAtomically(sessionPath, "session-json", CancellationToken.None);

        FileSystemAssert.ForDirectory(ucliDirectoryPath).Exists();
        FileSystemAssert.ForDirectory(localDirectoryPath).Exists();
        FileSystemAssert.ForFile(gitIgnorePath).Exists();
        Assert.Equal(UcliStoragePathNames.LocalDirectoryName + "/" + Environment.NewLine, File.ReadAllText(gitIgnorePath));
        Assert.Equal("session-json", File.ReadAllText(sessionPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EnsureStorageDirectoryExists_WhenGitIgnoreAlreadyExists_DoesNotOverwriteExistingContents ()
    {
        using var scope = TestDirectories.CreateTempScope("contracts-storage", "ensure-directory-existing-gitignore");
        var storageRoot = scope.CreateDirectory("Repo");
        var gitIgnorePath = Path.Combine(
            storageRoot,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.GitIgnoreFileName);
        var indexDirectoryPath = UcliStoragePathResolver.ResolveIndexDirectory(storageRoot, "fingerprint");
        scope.WriteFile(
            Path.Combine("Repo", UcliStoragePathNames.UcliDirectoryName, UcliStoragePathNames.GitIgnoreFileName),
            "legacy/" + Environment.NewLine);

        FileUtilities.EnsureStorageDirectoryExists(indexDirectoryPath);

        FileSystemAssert.ForDirectory(indexDirectoryPath).Exists();
        Assert.Equal("legacy/" + Environment.NewLine, File.ReadAllText(gitIgnorePath));
    }
}
