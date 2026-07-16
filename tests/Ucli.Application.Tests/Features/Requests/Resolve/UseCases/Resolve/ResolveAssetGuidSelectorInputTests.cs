namespace MackySoft.Ucli.Application.Tests.Features.Requests.Resolve.UseCases.Resolve;

public sealed class ResolveAssetGuidSelectorInputTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenAssetGuidIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ResolveAssetGuidSelectorInput(Guid.Empty));

        Assert.Equal("assetGuid", exception.ParamName);
    }
}
