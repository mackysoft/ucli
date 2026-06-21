using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexEnumContractTests
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
}
