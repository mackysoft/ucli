using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Adds a machine-readable semantic constraint to one operation input property or value type. </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property, AllowMultiple = true)]
public sealed class UcliInputConstraintAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliInputConstraintAttribute" /> class. </summary>
    /// <param name="kind"> The input constraint kind. </param>
    public UcliInputConstraintAttribute (UcliOperationInputConstraintKind kind)
    {
        Kind = kind;
    }

    /// <summary> Gets the input constraint kind. </summary>
    public UcliOperationInputConstraintKind Kind { get; }

    /// <summary> Gets or sets the asset kind parameter. </summary>
    public UcliOperationAssetKind AssetKind { get; set; }

    /// <summary> Gets or sets the reference target kind parameter. </summary>
    public UcliOperationReferenceTargetKind TargetKind { get; set; }

    /// <summary> Gets or sets the type kind parameter. </summary>
    public UcliOperationTypeKind TypeKind { get; set; }

    /// <summary> Gets or sets the serialized property access parameter. </summary>
    public UcliOperationSerializedPropertyAccess Access { get; set; }

    /// <summary> Gets or sets the inclusive minimum range value. Omit by leaving this value as <see cref="double.NaN" />. </summary>
    public double Min { get; set; } = double.NaN;

    /// <summary> Gets or sets the inclusive maximum range value. Omit by leaving this value as <see cref="double.NaN" />. </summary>
    public double Max { get; set; } = double.NaN;

    internal UcliOperationInputConstraintContract ToContract ()
    {
        return new UcliOperationInputConstraintContract(Kind)
        {
            AssetKind = AssetKind == UcliOperationAssetKind.Unspecified ? null : UcliOperationAssetKindCodec.ToValue(AssetKind),
            TargetKind = TargetKind == UcliOperationReferenceTargetKind.Unspecified ? null : UcliOperationReferenceTargetKindCodec.ToValue(TargetKind),
            TypeKind = TypeKind == UcliOperationTypeKind.Unspecified ? null : UcliOperationTypeKindCodec.ToValue(TypeKind),
            Access = Access == UcliOperationSerializedPropertyAccess.Unspecified ? null : UcliOperationSerializedPropertyAccessCodec.ToValue(Access),
            Min = double.IsNaN(Min) ? null : Min,
            Max = double.IsNaN(Max) ? null : Max,
        };
    }
}
