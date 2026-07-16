using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Operations;

public sealed class UcliInputConstraintAttributeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenNamedPropertiesAreUnset_OmitsAllOptionalParameters ()
    {
        var attribute = new UcliInputConstraintAttribute(UcliOperationInputConstraintKind.NonEmpty);

        var contract = UcliOperationInputConstraintContractMapper.Map(attribute);

        Assert.Null(contract.AssetKind);
        Assert.Null(contract.TargetKind);
        Assert.Null(contract.TypeKind);
        Assert.Null(contract.Access);
        Assert.Null(contract.Min);
        Assert.Null(contract.Max);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenNamedPropertiesAreSet_EmitsAssignedParameters ()
    {
        var attribute = new UcliInputConstraintAttribute(UcliOperationInputConstraintKind.Range)
        {
            AssetKind = UcliOperationAssetKind.Scene,
            TargetKind = UcliOperationReferenceTargetKind.GameObject,
            TypeKind = UcliOperationTypeKind.Component,
            Access = UcliOperationSerializedPropertyAccess.Write,
            Min = 0,
            Max = 10,
        };

        var contract = UcliOperationInputConstraintContractMapper.Map(attribute);

        Assert.Equal("scene", contract.AssetKind);
        Assert.Equal("gameObject", contract.TargetKind);
        Assert.Equal("component", contract.TypeKind);
        Assert.Equal("write", contract.Access);
        Assert.Equal(0, contract.Min);
        Assert.Equal(10, contract.Max);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EnumNamedProperties_WhenAssignedUndefinedValue_ThrowArgumentOutOfRangeException ()
    {
        var attribute = new UcliInputConstraintAttribute(UcliOperationInputConstraintKind.NonEmpty);
        var assignments = new (Action Assign, string ParameterName)[]
        {
            (() => attribute.AssetKind = default, "AssetKind"),
            (() => attribute.TargetKind = default, "TargetKind"),
            (() => attribute.TypeKind = default, "TypeKind"),
            (() => attribute.Access = default, "Access"),
        };

        Assert.All(
            assignments,
            assignment => Assert.Equal(
                assignment.ParameterName,
                Assert.Throws<ArgumentOutOfRangeException>(assignment.Assign).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RangeNamedProperties_WhenAssignedNonFiniteValue_ThrowArgumentOutOfRangeException ()
    {
        var attribute = new UcliInputConstraintAttribute(UcliOperationInputConstraintKind.Range);
        var assignments = new (Action Assign, string ParameterName)[]
        {
            (() => attribute.Min = double.NaN, "Min"),
            (() => attribute.Min = double.NegativeInfinity, "Min"),
            (() => attribute.Max = double.NaN, "Max"),
            (() => attribute.Max = double.PositiveInfinity, "Max"),
        };

        Assert.All(
            assignments,
            assignment => Assert.Equal(
                assignment.ParameterName,
                Assert.Throws<ArgumentOutOfRangeException>(assignment.Assign).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            static () => new UcliInputConstraintAttribute((UcliOperationInputConstraintKind)(-1)));

        Assert.Equal("kind", exception.ParamName);
    }
}
