namespace MackySoft.Ucli.Tests;

using MackySoft.Ucli.UnityProject.Resolution;

public sealed class LinuxUnityEditorSearchRootSourceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AppendSearchRoots_OnLinux_IncludesCaseVariantOptRoots ()
    {
        var source = new LinuxUnityEditorSearchRootSource();
        var searchRootBuilder = new UnityEditorSearchRootBuilder(StringComparer.Ordinal);
        source.AppendSearchRoots(searchRootBuilder);
        var roots = searchRootBuilder.ToArray();

        if (source.IsSupportedCurrentPlatform)
        {
            Assert.Contains("/opt/Unity/Hub/Editor", roots, StringComparer.Ordinal);
            Assert.Contains("/opt/unity/hub/editor", roots, StringComparer.Ordinal);
            return;
        }

        Assert.Empty(roots);
    }
}