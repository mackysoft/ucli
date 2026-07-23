namespace MackySoft.Ucli.Tests;

using MackySoft.FileSystem;
using MackySoft.Ucli.UnityIntegration.Resolution;

public sealed class UnityEditorInstallationSearchRootsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void GetSearchRoots_ReturnsCachedInstance ()
    {
        var first = UnityEditorInstallationSearchRoots.GetSearchRoots();
        var second = UnityEditorInstallationSearchRoots.GetSearchRoots();

        Assert.Same(first, second);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetSearchRoots_OnKnownPlatform_ReturnsPlatformRoots ()
    {
        var roots = UnityEditorInstallationSearchRoots.GetSearchRoots();

        if (OperatingSystem.IsMacOS())
        {
            Assert.Contains(AbsolutePath.Parse("/Applications/Unity/Hub/Editor"), roots);
            Assert.Contains(AbsolutePath.Parse("/Applications/Unity/Editor"), roots);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            Assert.Contains(AbsolutePath.Parse("/opt/Unity/Hub/Editor"), roots);
            Assert.Contains(AbsolutePath.Parse("/opt/unity/hub/editor"), roots);
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Assert.NotEmpty(roots);
            return;
        }

        Assert.Empty(roots);
    }
}
