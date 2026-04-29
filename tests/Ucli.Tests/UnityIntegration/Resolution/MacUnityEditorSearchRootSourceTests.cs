namespace MackySoft.Ucli.Tests;

using MackySoft.Ucli.UnityIntegration.Resolution;

public sealed class MacUnityEditorSearchRootSourceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AppendSearchRoots_OnMac_IncludesApplicationsRoots ()
    {
        var source = new MacUnityEditorSearchRootSource();
        var searchRootBuilder = new UnityEditorSearchRootBuilder(StringComparer.Ordinal);
        source.AppendSearchRoots(searchRootBuilder);
        var roots = searchRootBuilder.ToArray();

        if (source.IsSupportedCurrentPlatform)
        {
            Assert.Contains("/Applications/Unity/Hub/Editor", roots, StringComparer.Ordinal);
            Assert.Contains("/Applications/Unity/Editor", roots, StringComparer.Ordinal);
            return;
        }

        Assert.Empty(roots);
    }
}
