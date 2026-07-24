using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Tests.Paths;

public sealed class UcliPortablePathAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryFormat_WithNestedPath_ReturnsSlashSeparatedText ()
    {
        var path = RootRelativePath.Parse("Assets/Scenes/Main.unity");

        var succeeded = UcliPortablePathAdapter.TryFormat(path, out var portablePath);

        Assert.True(succeeded);
        Assert.Equal("Assets/Scenes/Main.unity", portablePath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryFormat_WithRoot_ReturnsNoPortablePath ()
    {
        var succeeded = UcliPortablePathAdapter.TryFormat(
            RootRelativePath.Parse("."),
            out var portablePath);

        Assert.False(succeeded);
        Assert.Null(portablePath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryFormat_OnUnixWithLiteralBackslash_ReturnsNoPortablePath ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = RootRelativePath.Parse(@"Assets/literal\name.txt");

        var succeeded = UcliPortablePathAdapter.TryFormat(path, out var portablePath);

        Assert.False(succeeded);
        Assert.Null(portablePath);
    }
}
