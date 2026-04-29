namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;

public sealed class TestDirectoryScopeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void GetDirectory_DoesNotCreateDirectory_WhenOnlyResolved ()
    {
        using var scope = TestDirectories.CreateTempScope("test-directory-scope", "at-resolve-only");
        var childScope = scope.GetDirectory("nested");

        Assert.Equal(Path.Combine(scope.FullPath, "nested"), childScope.FullPath);
        Assert.False(Directory.Exists(childScope.FullPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetDirectory_WriteFile_WritesUnderChildScopeRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("test-directory-scope", "at-write-file");
        var childScope = scope.GetDirectory("configs");

        var filePath = childScope.WriteFile("settings.json", "{\"enabled\":true}");

        Assert.Equal(Path.Combine(scope.FullPath, "configs", "settings.json"), filePath);
        Assert.True(File.Exists(filePath));
        Assert.Equal("{\"enabled\":true}", File.ReadAllText(filePath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetDirectory_AcceptsNestedRelativePath ()
    {
        using var scope = TestDirectories.CreateTempScope("test-directory-scope", "at-nested-path");

        var directoryPath = scope.GetDirectory(Path.Combine("a", "b")).CreateDirectory("c");

        Assert.Equal(Path.Combine(scope.FullPath, "a", "b", "c"), directoryPath);
        Assert.True(Directory.Exists(directoryPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetDirectory_Throws_WhenPathIsRooted ()
    {
        using var scope = TestDirectories.CreateTempScope("test-directory-scope", "at-rooted-path");
        var rootedPath = Path.Combine(Path.GetPathRoot(scope.FullPath)!, "outside.txt");

        Assert.Throws<ArgumentException>(() => scope.GetDirectory(rootedPath));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("../outside.txt")]
    [InlineData("nested//file.txt")]
    [InlineData("nested/ /file.txt")]
    public void GetDirectory_Throws_WhenPathIsInvalid (string relativePath)
    {
        using var scope = TestDirectories.CreateTempScope("test-directory-scope", "at-invalid-segment");

        Assert.Throws<ArgumentException>(() => scope.GetDirectory(relativePath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Dispose_ChildScope_DoesNotDeleteRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("test-directory-scope", "dispose-child");
        var childScope = scope.GetDirectory("nested");
        var leafDirectoryPath = childScope.CreateDirectory("leaf");

        childScope.Dispose();

        Assert.True(Directory.Exists(scope.FullPath));
        Assert.True(Directory.Exists(leafDirectoryPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Preserve_FromChildScope_PreservesRootDeletion ()
    {
        var scope = TestDirectories.CreateTempScope("test-directory-scope", "preserve-child");
        var rootDirectoryPath = scope.FullPath;
        scope.GetDirectory("nested").CreateDirectory("leaf");
        scope.GetDirectory("nested").Preserve();

        scope.Dispose();

        try
        {
            Assert.True(Directory.Exists(rootDirectoryPath));
        }
        finally
        {
            if (Directory.Exists(rootDirectoryPath))
            {
                Directory.Delete(rootDirectoryPath, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Dispose_DeletesScopeRootDirectory ()
    {
        string rootDirectoryPath;
        using (var scope = TestDirectories.CreateTempScope("test-directory-scope", "dispose-root"))
        {
            rootDirectoryPath = scope.FullPath;
            scope.CreateDirectory("nested");

            Assert.True(Directory.Exists(rootDirectoryPath));
        }

        Assert.False(Directory.Exists(rootDirectoryPath));
    }
}
