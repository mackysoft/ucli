namespace MackySoft.Ucli.Tests;

using MackySoft.Ucli.UnityProject.Resolution;

public sealed class DefaultUnityEditorSearchRootProviderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void GetSearchRoots_OnCaseSensitivePlatform_KeepsCaseVariantLinuxRoots ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var provider = new DefaultUnityEditorSearchRootProvider();

        var roots = provider.GetSearchRoots();

        Assert.Contains("/opt/Unity/Hub/Editor", roots, StringComparer.Ordinal);
        Assert.Contains("/opt/unity/hub/editor", roots, StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetSearchRoots_OnWindows_DeduplicatesCaseVariantLinuxRoots ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var provider = new DefaultUnityEditorSearchRootProvider();

        var roots = provider.GetSearchRoots();
        var caseVariantCount = roots.Count(path =>
            string.Equals(path, "/opt/Unity/Hub/Editor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/opt/unity/hub/editor", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(1, caseVariantCount);
    }
}
