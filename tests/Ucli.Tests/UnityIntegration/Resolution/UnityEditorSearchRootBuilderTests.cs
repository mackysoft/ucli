namespace MackySoft.Ucli.Tests;

using MackySoft.FileSystem;
using MackySoft.Ucli.UnityIntegration.Resolution;

public sealed class UnityEditorSearchRootBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Add_WhenRootPathIsNull_ThrowsArgumentNullException ()
    {
        var builder = new UnityEditorSearchRootBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.Add(null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Add_WhenRootPathIsDuplicated_KeepsFirstOccurrence ()
    {
        var builder = new UnityEditorSearchRootBuilder();

        var root = AbsolutePath.Parse(Path.GetFullPath("Root"));
        var another = AbsolutePath.Parse(Path.GetFullPath("Another"));
        builder.Add(root);
        builder.Add(another);
        builder.Add(root);

        Assert.Equal(
            new[]
            {
                root,
                another,
            },
            builder.ToArray());
    }
}
