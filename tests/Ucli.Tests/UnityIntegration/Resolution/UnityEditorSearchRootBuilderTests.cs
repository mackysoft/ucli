namespace MackySoft.Ucli.Tests;

using MackySoft.Ucli.UnityIntegration.Resolution;

public sealed class UnityEditorSearchRootBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Add_WhenRootPathIsNullOrWhitespace_ThrowsArgumentException ()
    {
        var builder = new UnityEditorSearchRootBuilder(StringComparer.Ordinal);

        Assert.ThrowsAny<ArgumentException>(() => builder.Add(null));
        Assert.ThrowsAny<ArgumentException>(() => builder.Add(string.Empty));
        Assert.ThrowsAny<ArgumentException>(() => builder.Add(" "));
    }
}