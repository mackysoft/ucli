namespace MackySoft.Ucli.Contracts.Tests.Resources;

public sealed class UcliTouchedResourceKindNamesTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ExposeExpectedLiterals ()
    {
        Assert.Equal("scene", UcliTouchedResourceKindNames.Scene);
        Assert.Equal("prefab", UcliTouchedResourceKindNames.Prefab);
        Assert.Equal("asset", UcliTouchedResourceKindNames.Asset);
        Assert.Equal("projectSettings", UcliTouchedResourceKindNames.ProjectSettings);
    }
}
