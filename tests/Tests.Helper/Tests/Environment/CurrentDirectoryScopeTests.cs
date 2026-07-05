namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;

[Collection(EnvironmentStateTestCollection.Name)]
public sealed class CurrentDirectoryScopeTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Dispose_RestoresOriginalCurrentDirectory ()
    {
        var originalCurrentDirectory = Environment.CurrentDirectory;

        using var scope = TestDirectories.CreateTempScope(
            "environment-state",
            "current-directory-restore");
        var scopedDirectory = scope.CreateDirectory("workspace");

        using (new CurrentDirectoryScope(scopedDirectory))
        {
            FileSystemAssert.ForPath(Environment.CurrentDirectory).EqualsNormalized(scopedDirectory);
        }

        FileSystemAssert.ForPath(Environment.CurrentDirectory).EqualsNormalized(originalCurrentDirectory);
    }
}
