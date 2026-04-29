using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class UcliLocalStorageBootstrapperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void EnsureInitialized_WhenTargetIsUnderUcliLocal_BootstrapsSharedStorage ()
    {
        using var scope = TestDirectories.CreateTempScope("contracts-storage", "bootstrap-local-storage");
        var storageRoot = scope.CreateDirectory("Repo");
        var indexDirectoryPath = UcliStoragePathResolver.ResolveIndexDirectory(storageRoot, "fingerprint");
        var ucliDirectoryPath = Path.Combine(storageRoot, UcliStoragePathNames.UcliDirectoryName);
        var localDirectoryPath = Path.Combine(ucliDirectoryPath, UcliStoragePathNames.LocalDirectoryName);
        var gitIgnorePath = Path.Combine(ucliDirectoryPath, UcliStoragePathNames.GitIgnoreFileName);

        UcliLocalStorageBootstrapper.EnsureInitialized(indexDirectoryPath);
        Directory.CreateDirectory(indexDirectoryPath);

        FileSystemAssert.ForDirectory(ucliDirectoryPath).Exists();
        FileSystemAssert.ForDirectory(localDirectoryPath).Exists();
        FileSystemAssert.ForDirectory(indexDirectoryPath).Exists();
        FileSystemAssert.ForFile(gitIgnorePath).Exists();
        Assert.Equal(
            UcliLocalStorageBootstrapper.LocalDirectoryIgnoreEntry + Environment.NewLine,
            File.ReadAllText(gitIgnorePath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EnsureInitialized_WhenGitIgnoreAlreadyExists_DoesNotOverwriteExistingContents ()
    {
        using var scope = TestDirectories.CreateTempScope("contracts-storage", "bootstrap-existing-gitignore");
        var storageRoot = scope.CreateDirectory("Repo");
        var gitIgnorePath = Path.Combine(
            storageRoot,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.GitIgnoreFileName);
        var indexDirectoryPath = UcliStoragePathResolver.ResolveIndexDirectory(storageRoot, "fingerprint");
        scope.WriteFile(
            Path.Combine("Repo", UcliStoragePathNames.UcliDirectoryName, UcliStoragePathNames.GitIgnoreFileName),
            "legacy/" + Environment.NewLine);

        UcliLocalStorageBootstrapper.EnsureInitialized(indexDirectoryPath);
        Directory.CreateDirectory(indexDirectoryPath);

        FileSystemAssert.ForDirectory(indexDirectoryPath).Exists();
        Assert.Equal("legacy/" + Environment.NewLine, File.ReadAllText(gitIgnorePath));
    }
}
