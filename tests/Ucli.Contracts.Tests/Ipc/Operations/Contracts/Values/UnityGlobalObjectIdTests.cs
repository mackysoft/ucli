using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations.Contracts.Values;

public sealed class UnityGlobalObjectIdTests
{
    public static TheoryData<string> InvalidValues => new()
    {
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
    public void Constructor_WhenValueIdentifiesBuiltInAsset_StoresTypedKind ()
    {
        var value = new UnityGlobalObjectId(
            "GlobalObjectId_V1-4-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-1-0");

        Assert.Equal(UnityGlobalObjectIdKind.BuiltInAsset, value.Kind);
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
        Assert.Equal(UnityGlobalObjectIdKind.SceneObject, value.Kind);
        Assert.Equal(Guid.ParseExact("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "N"), value.AssetGuid.Guid);
        Assert.Equal(902906726UL, value.TargetObjectId);
        Assert.Equal(0UL, value.TargetPrefabId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenObjectIdentifiersUseMaximumValues_PreservesCanonicalValue ()
    {
        const string canonicalValue =
            "GlobalObjectId_V1-3-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-18446744073709551615-18446744073709551615";

        var value = new UnityGlobalObjectId(canonicalValue);

        Assert.Equal(canonicalValue, value.Value);
        Assert.Equal(ulong.MaxValue, value.TargetObjectId);
        Assert.Equal(ulong.MaxValue, value.TargetPrefabId);
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

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenValueIsInvalid_ReturnsFalseWithoutValue ()
    {
        var result = UnityGlobalObjectId.TryParse("not-a-global-object-id", out var globalObjectId);

        Assert.False(result);
        Assert.Null(globalObjectId);
    }
}
