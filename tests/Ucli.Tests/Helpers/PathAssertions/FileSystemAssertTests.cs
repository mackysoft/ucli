namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;
using Xunit.Sdk;

public sealed class FileSystemAssertTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ForDirectory_CanAssertRecursiveStructure ()
    {
        // Arrange
        using var rootScope = TestDirectories.CreateTempScope("filesystem-assert", "recursive");
        rootScope.CreateDirectory(Path.Combine("run", "logs"));
        rootScope.WriteFile(Path.Combine("run", "meta.json"), "{}");
        rootScope.WriteFile(Path.Combine("run", "logs", "editor.log"), "log");

        // Act + Assert
        FileSystemAssert.ForDirectory(rootScope.FullPath)
            .HasDirectory(
                "run",
                static run => run
                    .HasFile("meta.json")
                    .HasDirectory(
                        "logs",
                        static logs => logs
                            .HasFile("editor.log")
                            .MatchesExactly())
                    .MatchesExactly())
            .MatchesExactly();
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesExactly_Throws_WhenUnexpectedEntryExists ()
    {
        // Arrange
        using var rootScope = TestDirectories.CreateTempScope("filesystem-assert", "unexpected-entry");
        rootScope.WriteFile("expected.txt", "ok");
        rootScope.WriteFile("extra.txt", "extra");

        // Act
        var exception = Assert.Throws<XunitException>(
            () => FileSystemAssert.ForDirectory(rootScope.FullPath)
                .HasFile("expected.txt")
                .MatchesExactly());

        // Assert
        Assert.Contains("unexpected", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("extra.txt", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HasFile_Throws_WhenRequiredFileIsMissing ()
    {
        // Arrange
        using var rootScope = TestDirectories.CreateTempScope("filesystem-assert", "missing-file");

        // Act
        var exception = Assert.Throws<XunitException>(
            () => FileSystemAssert.ForDirectory(rootScope.FullPath).HasFile("missing.txt"));

        // Assert
        Assert.Contains("missing.txt", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsUnderDirectory_Throws_WhenPathIsOutsideParent ()
    {
        // Arrange
        using var parentRootScope = TestDirectories.CreateTempScope("filesystem-assert", "parent");
        using var outsideRootScope = TestDirectories.CreateTempScope("filesystem-assert", "outside");
        var outsideFilePath = outsideRootScope.WriteFile("result.json", "{}");

        // Act
        var exception = Assert.Throws<XunitException>(
            () => FileSystemAssert.ForPath(outsideFilePath).IsUnderDirectory(parentRootScope.FullPath));

        // Assert
        Assert.Contains("outside", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HasExtension_ValidatesFileExtension ()
    {
        // Arrange
        using var rootScope = TestDirectories.CreateTempScope("filesystem-assert", "extension");
        var filePath = rootScope.WriteFile("summary.json", "{}");

        // Act + Assert
        FileSystemAssert.ForFile(filePath)
            .Exists()
            .HasExtension("json")
            .HasFileName("summary.json");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EqualsNormalized_TreatsSymlinkAndTargetAsEquivalent ()
    {
        // Arrange
        using var rootScope = TestDirectories.CreateTempScope("filesystem-assert", "normalize-symlink");
        var targetDirectoryPath = rootScope.CreateDirectory("target");
        var targetFilePath = rootScope.WriteFile(Path.Combine("target", "profile.json"), "{}");
        var symbolicLinkDirectoryPath = Path.Combine(rootScope.FullPath, "alias");
        if (!TryCreateDirectorySymbolicLink(symbolicLinkDirectoryPath, targetDirectoryPath))
        {
            return;
        }

        var linkedFilePath = Path.Combine(symbolicLinkDirectoryPath, "profile.json");

        // Act + Assert
        FileSystemAssert.ForPath(linkedFilePath)
            .EqualsNormalized(targetFilePath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HasDirectory_Throws_WhenChildNameContainsDirectorySeparator ()
    {
        // Arrange
        using var rootScope = TestDirectories.CreateTempScope("filesystem-assert", "invalid-child-name");

        // Act
        var exception = Assert.Throws<XunitException>(
            () => FileSystemAssert.ForDirectory(rootScope.FullPath).HasDirectory("child/nested"));

        // Assert
        Assert.Contains("directory separators", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DoesNotExist_Throws_WhenPathAlreadyExists ()
    {
        // Arrange
        using var rootScope = TestDirectories.CreateTempScope("filesystem-assert", "does-not-exist");
        var filePath = rootScope.WriteFile("exists.txt", "exists");

        // Act
        var exception = Assert.Throws<XunitException>(
            () => FileSystemAssert.ForPath(filePath).DoesNotExist());

        // Assert
        Assert.Contains("expected to not exist", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateDirectorySymbolicLink (string symbolicLinkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(symbolicLinkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
