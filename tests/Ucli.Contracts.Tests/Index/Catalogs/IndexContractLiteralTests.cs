using MackySoft.Ucli.Contracts.Index;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexContractLiteralTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IndexSchemaKind_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)IndexSchemaKind.Comp);
        Assert.Equal(1, (int)IndexSchemaKind.Asset);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexSchemaKindContractLiteral_ToValue_ReturnsStableLiterals ()
    {
        Assert.Equal("comp", ContractLiteralCodec.ToValue(IndexSchemaKind.Comp));
        Assert.Equal("asset", ContractLiteralCodec.ToValue(IndexSchemaKind.Asset));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("comp", IndexSchemaKind.Comp)]
    [InlineData("COMP", IndexSchemaKind.Comp)]
    [InlineData("asset", IndexSchemaKind.Asset)]
    [InlineData("ASSET", IndexSchemaKind.Asset)]
    public void IndexSchemaKindContractLiteral_TryParse_ParsesCaseInsensitiveLiterals (
        string value,
        IndexSchemaKind expected)
    {
        var result = ContractLiteralInputParser.TryParseIgnoreCase<IndexSchemaKind>(value, out var parsed);

        Assert.True(result);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    public void IndexSchemaKindContractLiteral_TryParse_UnknownValue_ReturnsFalse (
        string value)
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<IndexSchemaKind>(value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexSchemaKindContractLiteral_TryParse_Null_ReturnsFalse ()
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<IndexSchemaKind>(null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexPropertyType_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)IndexPropertyType.Generic);
        Assert.Equal(1, (int)IndexPropertyType.Integer);
        Assert.Equal(2, (int)IndexPropertyType.Boolean);
        Assert.Equal(3, (int)IndexPropertyType.Float);
        Assert.Equal(4, (int)IndexPropertyType.String);
        Assert.Equal(5, (int)IndexPropertyType.Color);
        Assert.Equal(6, (int)IndexPropertyType.ObjectReference);
        Assert.Equal(7, (int)IndexPropertyType.LayerMask);
        Assert.Equal(8, (int)IndexPropertyType.Enum);
        Assert.Equal(9, (int)IndexPropertyType.Vector2);
        Assert.Equal(10, (int)IndexPropertyType.Vector3);
        Assert.Equal(11, (int)IndexPropertyType.Vector4);
        Assert.Equal(12, (int)IndexPropertyType.Rect);
        Assert.Equal(13, (int)IndexPropertyType.ArraySize);
        Assert.Equal(14, (int)IndexPropertyType.Character);
        Assert.Equal(15, (int)IndexPropertyType.AnimationCurve);
        Assert.Equal(16, (int)IndexPropertyType.Bounds);
        Assert.Equal(17, (int)IndexPropertyType.Gradient);
        Assert.Equal(18, (int)IndexPropertyType.Quaternion);
        Assert.Equal(19, (int)IndexPropertyType.ExposedReference);
        Assert.Equal(20, (int)IndexPropertyType.FixedBufferSize);
        Assert.Equal(21, (int)IndexPropertyType.Vector2Int);
        Assert.Equal(22, (int)IndexPropertyType.Vector3Int);
        Assert.Equal(23, (int)IndexPropertyType.RectInt);
        Assert.Equal(24, (int)IndexPropertyType.BoundsInt);
        Assert.Equal(25, (int)IndexPropertyType.ManagedReference);
        Assert.Equal(26, (int)IndexPropertyType.Hash128);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IndexPropertyType.Generic, "generic")]
    [InlineData(IndexPropertyType.Integer, "integer")]
    [InlineData(IndexPropertyType.Boolean, "boolean")]
    [InlineData(IndexPropertyType.Float, "float")]
    [InlineData(IndexPropertyType.String, "string")]
    [InlineData(IndexPropertyType.Color, "color")]
    [InlineData(IndexPropertyType.ObjectReference, "objectReference")]
    [InlineData(IndexPropertyType.LayerMask, "layerMask")]
    [InlineData(IndexPropertyType.Enum, "enum")]
    [InlineData(IndexPropertyType.Vector2, "vector2")]
    [InlineData(IndexPropertyType.Vector3, "vector3")]
    [InlineData(IndexPropertyType.Vector4, "vector4")]
    [InlineData(IndexPropertyType.Rect, "rect")]
    [InlineData(IndexPropertyType.ArraySize, "arraySize")]
    [InlineData(IndexPropertyType.Character, "character")]
    [InlineData(IndexPropertyType.AnimationCurve, "animationCurve")]
    [InlineData(IndexPropertyType.Bounds, "bounds")]
    [InlineData(IndexPropertyType.Gradient, "gradient")]
    [InlineData(IndexPropertyType.Quaternion, "quaternion")]
    [InlineData(IndexPropertyType.ExposedReference, "exposedReference")]
    [InlineData(IndexPropertyType.FixedBufferSize, "fixedBufferSize")]
    [InlineData(IndexPropertyType.Vector2Int, "vector2Int")]
    [InlineData(IndexPropertyType.Vector3Int, "vector3Int")]
    [InlineData(IndexPropertyType.RectInt, "rectInt")]
    [InlineData(IndexPropertyType.BoundsInt, "boundsInt")]
    [InlineData(IndexPropertyType.ManagedReference, "managedReference")]
    [InlineData(IndexPropertyType.Hash128, "hash128")]
    public void IndexPropertyTypeContractLiteral_ToValue_ReturnsStableLiterals (
        IndexPropertyType propertyType,
        string expectedLiteral)
    {
        Assert.Equal(expectedLiteral, ContractLiteralCodec.ToValue(propertyType));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("generic", IndexPropertyType.Generic)]
    [InlineData("GENERIC", IndexPropertyType.Generic)]
    [InlineData("objectReference", IndexPropertyType.ObjectReference)]
    [InlineData("managedReference", IndexPropertyType.ManagedReference)]
    [InlineData("hash128", IndexPropertyType.Hash128)]
    public void IndexPropertyTypeContractLiteral_TryParse_ParsesCaseInsensitiveLiterals (
        string value,
        IndexPropertyType expected)
    {
        var result = ContractLiteralInputParser.TryParseIgnoreCase<IndexPropertyType>(value, out var parsed);

        Assert.True(result);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    public void IndexPropertyTypeContractLiteral_TryParse_UnknownValue_ReturnsFalse (
        string value)
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<IndexPropertyType>(value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexPropertyTypeContractLiteral_TryParse_Null_ReturnsFalse ()
    {
        Assert.False(ContractLiteralInputParser.IsDefinedIgnoreCase<IndexPropertyType>(null));
    }
}
