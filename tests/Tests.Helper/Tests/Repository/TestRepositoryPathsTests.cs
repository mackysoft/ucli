using Xunit.Sdk;

namespace MackySoft.Tests;

public sealed class TestRepositoryPathsTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void EnumerateRegularFilesUnderDirectory_skips_reparse_directories ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "test-repository-paths",
            "skip-reparse-directories",
            DirectoryCleanupMode.BestEffort);
        var root = scope.CreateDirectory("root");
        var outside = scope.CreateDirectory("outside");
        var regularFile = Path.Combine(root, "regular.md");
        var outsideFile = Path.Combine(outside, "outside.md");
        File.WriteAllText(regularFile, string.Empty);
        File.WriteAllText(outsideFile, string.Empty);
        var linkPath = Path.Combine(root, "linked");
        if (!TestSymbolicLinks.TryCreateDirectory(linkPath, outside))
        {
            throw SkipException.ForSkip("Symbolic links are not supported by this test environment.");
        }

        var files = TestRepositoryPaths.EnumerateRegularFilesUnderDirectory(root).ToArray();
        var linkedOutsideFile = Path.Combine(linkPath, "outside.md");

        Assert.Contains(regularFile, files);
        Assert.DoesNotContain(outsideFile, files);
        Assert.DoesNotContain(linkedOutsideFile, files);
        Assert.DoesNotContain(files, file => file.StartsWith(linkPath + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void EnumerateRegularFilesUnderDirectory_rejects_reparse_root ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "test-repository-paths",
            "reject-reparse-root",
            DirectoryCleanupMode.BestEffort);
        var target = scope.CreateDirectory("target");
        var linkPath = Path.Combine(scope.FullPath, "linked-root");
        if (!TestSymbolicLinks.TryCreateDirectory(linkPath, target))
        {
            throw SkipException.ForSkip("Symbolic links are not supported by this test environment.");
        }

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestRepositoryPaths.EnumerateRegularFilesUnderDirectory(linkPath).ToArray());

        Assert.Contains("Root directory must not be a reparse point", exception.Message, StringComparison.Ordinal);
    }
}
