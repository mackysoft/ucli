using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Values;

public sealed class UnityGlobalObjectIdTests
{
    public static TheoryData<string> InvalidValues => new()
    {
        "not-a-global-object-id",
        "GlobalObjectId_V1-0-00000000000000000000000000000000-0-0",
        "GlobalObjectId_V1-5-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-1-0",
        "GlobalObjectId_V1-2-00000000000000000000000000000000-1-0",
        "GlobalObjectId_V1-2-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-1-0",
        "GlobalObjectId_V1-2-gggggggggggggggggggggggggggggggg-1-0",
        "GlobalObjectId_V1-2-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa--0",
        "GlobalObjectId_V1-2-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-18446744073709551616-0",
        "GlobalObjectId_V1-2-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-1-18446744073709551616",
        "GlobalObjectId_V1-2-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-1",
        "GlobalObjectId_V1-2-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-1-0-extra",
    };

    [Theory]
    [MemberData(nameof(InvalidValues))]
    [Trait("Size", "Small")]
    public void Constructor_WhenValueIsInvalid_ThrowsArgumentException (string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => new UnityGlobalObjectId(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenValueUsesEquivalentNonCanonicalText_StoresCanonicalValue ()
    {
        var value = new UnityGlobalObjectId(
            "GlobalObjectId_V1-02-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA-000902906726-000");

        Assert.Equal(
            "GlobalObjectId_V1-2-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-902906726-0",
            value.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenObjectIdentifiersUseMaximumValues_PreservesCanonicalValue ()
    {
        const string canonicalValue =
            "GlobalObjectId_V1-3-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-18446744073709551615-18446744073709551615";

        var value = new UnityGlobalObjectId(canonicalValue);

        Assert.Equal(canonicalValue, value.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenValueIsValid_ReturnsCanonicalTypedId ()
    {
        var result = UnityGlobalObjectId.TryParse(
            "GlobalObjectId_V1-02-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA-000902906726-000",
            out var globalObjectId);

        Assert.True(result);
        Assert.NotNull(globalObjectId);
        Assert.Equal(
            "GlobalObjectId_V1-2-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-902906726-0",
            globalObjectId.Value);
    }

    [Theory]
    [MemberData(nameof(InvalidValues))]
    [Trait("Size", "Small")]
    public void TryParse_WhenStructureIsInvalid_ReturnsFalseWithoutValue (string value)
    {
        var result = UnityGlobalObjectId.TryParse(value, out var globalObjectId);

        Assert.False(result);
        Assert.Null(globalObjectId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenValueViolatesCommonStringInvariant_ReturnsFalseWithoutValue ()
    {
        string?[] invalidValues =
        [
            null,
            string.Empty,
            " \t\r\n",
            " GlobalObjectId_V1-2-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-1-0",
            "GlobalObjectId_V1-2-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-1-0 ",
            "GlobalObjectId_V1-2-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-1-\ud800",
        ];

        foreach (var invalidValue in invalidValues)
        {
            var result = UnityGlobalObjectId.TryParse(invalidValue, out var globalObjectId);

            Assert.False(result);
            Assert.Null(globalObjectId);
        }
    }
}
