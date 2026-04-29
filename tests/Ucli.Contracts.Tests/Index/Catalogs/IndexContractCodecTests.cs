using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexContractCodecTests
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
    public void IndexSchemaKindCodec_ToValue_ReturnsStableLiterals ()
    {
        Assert.Equal(IndexSchemaKindValues.Comp, IndexSchemaKindCodec.ToValue(IndexSchemaKind.Comp));
        Assert.Equal(IndexSchemaKindValues.Asset, IndexSchemaKindCodec.ToValue(IndexSchemaKind.Asset));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("comp", IndexSchemaKind.Comp)]
    [InlineData("COMP", IndexSchemaKind.Comp)]
    [InlineData("asset", IndexSchemaKind.Asset)]
    [InlineData("ASSET", IndexSchemaKind.Asset)]
    public void IndexSchemaKindCodec_TryParse_ParsesCaseInsensitiveLiterals (
        string value,
        IndexSchemaKind expected)
    {
        var result = IndexSchemaKindCodec.TryParse(value, out var parsed);

        Assert.True(result);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    public void IndexSchemaKindCodec_TryParse_UnknownValue_ReturnsFalse (
        string value)
    {
        Assert.False(IndexSchemaKindCodec.TryParse(value, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexSchemaKindCodec_TryParse_Null_ReturnsFalse ()
    {
        Assert.False(IndexSchemaKindCodec.TryParse(null, out _));
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
    [InlineData(IndexPropertyType.Generic, IndexPropertyTypeValues.Generic)]
    [InlineData(IndexPropertyType.Integer, IndexPropertyTypeValues.Integer)]
    [InlineData(IndexPropertyType.Boolean, IndexPropertyTypeValues.Boolean)]
    [InlineData(IndexPropertyType.Float, IndexPropertyTypeValues.Float)]
    [InlineData(IndexPropertyType.String, IndexPropertyTypeValues.String)]
    [InlineData(IndexPropertyType.Color, IndexPropertyTypeValues.Color)]
    [InlineData(IndexPropertyType.ObjectReference, IndexPropertyTypeValues.ObjectReference)]
    [InlineData(IndexPropertyType.LayerMask, IndexPropertyTypeValues.LayerMask)]
    [InlineData(IndexPropertyType.Enum, IndexPropertyTypeValues.Enum)]
    [InlineData(IndexPropertyType.Vector2, IndexPropertyTypeValues.Vector2)]
    [InlineData(IndexPropertyType.Vector3, IndexPropertyTypeValues.Vector3)]
    [InlineData(IndexPropertyType.Vector4, IndexPropertyTypeValues.Vector4)]
    [InlineData(IndexPropertyType.Rect, IndexPropertyTypeValues.Rect)]
    [InlineData(IndexPropertyType.ArraySize, IndexPropertyTypeValues.ArraySize)]
    [InlineData(IndexPropertyType.Character, IndexPropertyTypeValues.Character)]
    [InlineData(IndexPropertyType.AnimationCurve, IndexPropertyTypeValues.AnimationCurve)]
    [InlineData(IndexPropertyType.Bounds, IndexPropertyTypeValues.Bounds)]
    [InlineData(IndexPropertyType.Gradient, IndexPropertyTypeValues.Gradient)]
    [InlineData(IndexPropertyType.Quaternion, IndexPropertyTypeValues.Quaternion)]
    [InlineData(IndexPropertyType.ExposedReference, IndexPropertyTypeValues.ExposedReference)]
    [InlineData(IndexPropertyType.FixedBufferSize, IndexPropertyTypeValues.FixedBufferSize)]
    [InlineData(IndexPropertyType.Vector2Int, IndexPropertyTypeValues.Vector2Int)]
    [InlineData(IndexPropertyType.Vector3Int, IndexPropertyTypeValues.Vector3Int)]
    [InlineData(IndexPropertyType.RectInt, IndexPropertyTypeValues.RectInt)]
    [InlineData(IndexPropertyType.BoundsInt, IndexPropertyTypeValues.BoundsInt)]
    [InlineData(IndexPropertyType.ManagedReference, IndexPropertyTypeValues.ManagedReference)]
    [InlineData(IndexPropertyType.Hash128, IndexPropertyTypeValues.Hash128)]
    public void IndexPropertyTypeCodec_ToValue_ReturnsStableLiterals (
        IndexPropertyType propertyType,
        string expectedLiteral)
    {
        Assert.Equal(expectedLiteral, IndexPropertyTypeCodec.ToValue(propertyType));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IndexPropertyTypeValues.Generic, IndexPropertyType.Generic)]
    [InlineData("GENERIC", IndexPropertyType.Generic)]
    [InlineData(IndexPropertyTypeValues.ObjectReference, IndexPropertyType.ObjectReference)]
    [InlineData(IndexPropertyTypeValues.ManagedReference, IndexPropertyType.ManagedReference)]
    [InlineData(IndexPropertyTypeValues.Hash128, IndexPropertyType.Hash128)]
    public void IndexPropertyTypeCodec_TryParse_ParsesCaseInsensitiveLiterals (
        string value,
        IndexPropertyType expected)
    {
        var result = IndexPropertyTypeCodec.TryParse(value, out var parsed);

        Assert.True(result);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    public void IndexPropertyTypeCodec_TryParse_UnknownValue_ReturnsFalse (
        string value)
    {
        Assert.False(IndexPropertyTypeCodec.TryParse(value, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexPropertyTypeCodec_TryParse_Null_ReturnsFalse ()
    {
        Assert.False(IndexPropertyTypeCodec.TryParse(null, out _));
    }
}
