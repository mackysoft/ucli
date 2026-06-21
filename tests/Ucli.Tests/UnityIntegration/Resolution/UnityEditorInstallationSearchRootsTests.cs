namespace MackySoft.Ucli.Tests;

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
            Assert.Contains("/Applications/Unity/Hub/Editor", roots, StringComparer.Ordinal);
            Assert.Contains("/Applications/Unity/Editor", roots, StringComparer.Ordinal);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            Assert.Contains("/opt/Unity/Hub/Editor", roots, StringComparer.Ordinal);
            Assert.Contains("/opt/unity/hub/editor", roots, StringComparer.Ordinal);
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
