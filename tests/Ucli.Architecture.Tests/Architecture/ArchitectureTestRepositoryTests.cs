using Xunit.Sdk;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class ArchitectureTestRepositoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void EnumerateRegularFilesUnderDirectory_skips_reparse_directories ()
    {
        using var scope = CreateTemporaryDirectory();
        var root = Path.Combine(scope.FullPath, "root");
        var outside = Path.Combine(scope.FullPath, "outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        var regularFile = Path.Combine(root, "regular.md");
        var outsideFile = Path.Combine(outside, "outside.md");
        File.WriteAllText(regularFile, string.Empty);
        File.WriteAllText(outsideFile, string.Empty);
        var linkPath = Path.Combine(root, "linked");
        if (!TryCreateDirectorySymbolicLink(linkPath, outside))
        {
            throw SkipException.ForSkip("Symbolic links are not supported by this test environment.");
        }

        var files = ArchitectureTestRepository.EnumerateRegularFilesUnderDirectory(root).ToArray();
        var linkedOutsideFile = Path.Combine(linkPath, "outside.md");

        Assert.Contains(regularFile, files);
        Assert.DoesNotContain(outsideFile, files);
        Assert.DoesNotContain(linkedOutsideFile, files);
        Assert.DoesNotContain(files, file => file.StartsWith(linkPath + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EnumerateRegularFilesUnderDirectory_rejects_reparse_root ()
    {
        using var scope = CreateTemporaryDirectory();
        var target = Path.Combine(scope.FullPath, "target");
        Directory.CreateDirectory(target);
        var linkPath = Path.Combine(scope.FullPath, "linked-root");
        if (!TryCreateDirectorySymbolicLink(linkPath, target))
        {
            throw SkipException.ForSkip("Symbolic links are not supported by this test environment.");
        }

        var exception = Assert.Throws<InvalidOperationException>(() => ArchitectureTestRepository.EnumerateRegularFilesUnderDirectory(linkPath).ToArray());

        Assert.Contains("Root directory must not be a reparse point", exception.Message, StringComparison.Ordinal);
    }

    private static TemporaryDirectory CreateTemporaryDirectory ()
    {
        var path = Path.Combine(Path.GetTempPath(), "ucli-architecture-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TemporaryDirectory(path);
    }

    private static bool TryCreateDirectorySymbolicLink (string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
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

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory (string fullPath)
        {
            FullPath = fullPath;
        }

        internal string FullPath { get; }

        public void Dispose ()
        {
            if (Directory.Exists(FullPath))
            {
                Directory.Delete(FullPath, recursive: true);
            }
        }
    }
}
