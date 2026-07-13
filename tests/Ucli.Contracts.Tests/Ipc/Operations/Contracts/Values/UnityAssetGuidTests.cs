using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations.Contracts.Values;

public sealed class UnityAssetGuidTests
{
    public static TheoryData<string> InvalidValues => new()
    {
        "00000000000000000000000000000000",
        "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "gggggggggggggggggggggggggggggggg",
    };

    [Theory]
    [MemberData(nameof(InvalidValues))]
    [Trait("Size", "Small")]
    public void Constructor_WhenValueIsNotCanonical_ThrowsArgumentException (string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => new UnityAssetGuid(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenValueIsCanonical_ReturnsTypedGuid ()
    {
        const string Value = "0123456789abcdef0123456789abcdef";

        var result = UnityAssetGuid.TryParse(Value, out var assetGuid);

        Assert.True(result);
        Assert.NotNull(assetGuid);
        Assert.Equal(Value, assetGuid.Value);
        Assert.Equal(Guid.ParseExact(Value, "N"), assetGuid.Guid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenValueIsInvalid_ReturnsFalseWithoutValue ()
    {
        var result = UnityAssetGuid.TryParse("not-an-asset-guid", out var assetGuid);

        Assert.False(result);
        Assert.Null(assetGuid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenValueUsesUppercaseNFormat_StoresCanonicalLowercaseValue ()
    {
        var assetGuid = new UnityAssetGuid("0123456789ABCDEF0123456789ABCDEF");

        Assert.Equal("0123456789abcdef0123456789abcdef", assetGuid.Value);
    }
}
