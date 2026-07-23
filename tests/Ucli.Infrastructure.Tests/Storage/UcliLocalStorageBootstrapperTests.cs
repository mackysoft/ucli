using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class UcliLocalStorageBootstrapperTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void EnsureInitialized_WhenTargetIsUnderUcliLocal_BootstrapsSharedStorage ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "bootstrap-local-storage");
        var storageRoot = AbsolutePath.Parse(scope.CreateDirectory("Repo"));
        var indexDirectoryPath = UcliStoragePathResolver.ResolveIndexDirectory(
            storageRoot,
            new ProjectFingerprint("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"));
        var ucliDirectoryPath = Path.Combine(storageRoot.Value, UcliStoragePathNames.UcliDirectoryName);
        var localDirectoryPath = Path.Combine(ucliDirectoryPath, UcliStoragePathNames.LocalDirectoryName);
        var gitIgnorePath = Path.Combine(ucliDirectoryPath, UcliStoragePathNames.GitIgnoreFileName);

        UcliLocalStorageBootstrapper.EnsureInitialized(indexDirectoryPath);
        Directory.CreateDirectory(indexDirectoryPath.Value);

        FileSystemAssert.ForDirectory(ucliDirectoryPath).Exists();
        FileSystemAssert.ForDirectory(localDirectoryPath).Exists();
        FileSystemAssert.ForDirectory(indexDirectoryPath.Value).Exists();
        FileSystemAssert.ForFile(gitIgnorePath).Exists();
        Assert.Equal(
            UcliLocalStorageBootstrapper.LocalDirectoryIgnoreEntry + Environment.NewLine,
            File.ReadAllText(gitIgnorePath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void EnsureInitialized_WhenGitIgnoreAlreadyExists_DoesNotOverwriteExistingContents ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "bootstrap-existing-gitignore");
        var storageRoot = AbsolutePath.Parse(scope.CreateDirectory("Repo"));
        var gitIgnorePath = Path.Combine(
            storageRoot.Value,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.GitIgnoreFileName);
        var indexDirectoryPath = UcliStoragePathResolver.ResolveIndexDirectory(
            storageRoot,
            new ProjectFingerprint("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"));
        scope.WriteFile(
            Path.Combine("Repo", UcliStoragePathNames.UcliDirectoryName, UcliStoragePathNames.GitIgnoreFileName),
            "legacy/" + Environment.NewLine);

        UcliLocalStorageBootstrapper.EnsureInitialized(indexDirectoryPath);
        Directory.CreateDirectory(indexDirectoryPath.Value);

        FileSystemAssert.ForDirectory(indexDirectoryPath.Value).Exists();
        Assert.Equal("legacy/" + Environment.NewLine, File.ReadAllText(gitIgnorePath));
    }
}
