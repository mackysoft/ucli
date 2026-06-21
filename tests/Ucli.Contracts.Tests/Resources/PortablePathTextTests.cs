namespace MackySoft.Ucli.Contracts.Tests.Resources;

public sealed class PortablePathTextTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ToSlashSeparated_ReplacesBackslashesWithSlashes ()
    {
        var result = PortablePathText.ToSlashSeparated(@"a\b/c");

        Assert.Equal("a/b/c", result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TrimTrailingDirectorySeparators_RemovesSlashAndBackslashSuffixes ()
    {
        var result = PortablePathText.TrimTrailingDirectorySeparators(@"output/root/\");

        Assert.Equal("output/root", result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToSlashSeparated_Throws_WhenValueIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = PortablePathText.ToSlashSeparated(null!);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TrimTrailingDirectorySeparators_Throws_WhenValueIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = PortablePathText.TrimTrailingDirectorySeparators(null!);
        });
    }
}
