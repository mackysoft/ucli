using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Adds a machine-readable semantic constraint to one operation input property or value type. </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property, AllowMultiple = true)]
public sealed class UcliInputConstraintAttribute : Attribute
{
    private UcliOperationAssetKind assetKind;
    private UcliOperationReferenceTargetKind targetKind;
    private UcliOperationTypeKind typeKind;
    private UcliOperationSerializedPropertyAccess access;
    private double min;
    private double max;

    /// <summary> Initializes a new instance of the <see cref="UcliInputConstraintAttribute" /> class. </summary>
    /// <param name="kind"> The input constraint kind. </param>
    public UcliInputConstraintAttribute (UcliOperationInputConstraintKind kind)
    {
        if (!ContractLiteralCodec.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Input constraint kind must be defined by the operation contract.");
        }

        Kind = kind;
    }

    /// <summary> Gets the input constraint kind. </summary>
    public UcliOperationInputConstraintKind Kind { get; }

    /// <summary> Gets or sets the asset kind parameter. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the assigned value is undefined. </exception>
    public UcliOperationAssetKind AssetKind
    {
        get => assetKind;
        set
        {
            if (!ContractLiteralCodec.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(AssetKind), value, "Asset kind must be defined by the operation contract.");
            }

            assetKind = value;
            HasAssetKind = true;
        }
    }

    /// <summary> Gets or sets the reference target kind parameter. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the assigned value is undefined. </exception>
    public UcliOperationReferenceTargetKind TargetKind
    {
        get => targetKind;
        set
        {
            if (!ContractLiteralCodec.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(TargetKind), value, "Reference target kind must be defined by the operation contract.");
            }

            targetKind = value;
            HasTargetKind = true;
        }
    }

    /// <summary> Gets or sets the type kind parameter. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the assigned value is undefined. </exception>
    public UcliOperationTypeKind TypeKind
    {
        get => typeKind;
        set
        {
            if (!ContractLiteralCodec.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(TypeKind), value, "Type kind must be defined by the operation contract.");
            }

            typeKind = value;
            HasTypeKind = true;
        }
    }

    /// <summary> Gets or sets the serialized property access parameter. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the assigned value is undefined. </exception>
    public UcliOperationSerializedPropertyAccess Access
    {
        get => access;
        set
        {
            if (!ContractLiteralCodec.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(Access), value, "Serialized property access must be defined by the operation contract.");
            }

            access = value;
            HasAccess = true;
        }
    }

    /// <summary> Gets or sets the inclusive minimum range value. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the assigned value is not finite. </exception>
    public double Min
    {
        get => min;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(Min), value, "Minimum range value must be finite.");
            }

            min = value;
            HasMin = true;
        }
    }

    /// <summary> Gets or sets the inclusive maximum range value. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the assigned value is not finite. </exception>
    public double Max
    {
        get => max;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(Max), value, "Maximum range value must be finite.");
            }

            max = value;
            HasMax = true;
        }
    }

    /// <summary> Gets whether <see cref="AssetKind" /> was explicitly assigned. </summary>
    internal bool HasAssetKind { get; private set; }

    /// <summary> Gets whether <see cref="TargetKind" /> was explicitly assigned. </summary>
    internal bool HasTargetKind { get; private set; }

    /// <summary> Gets whether <see cref="TypeKind" /> was explicitly assigned. </summary>
    internal bool HasTypeKind { get; private set; }

    /// <summary> Gets whether <see cref="Access" /> was explicitly assigned. </summary>
    internal bool HasAccess { get; private set; }

    /// <summary> Gets whether <see cref="Min" /> was explicitly assigned. </summary>
    internal bool HasMin { get; private set; }

    /// <summary> Gets whether <see cref="Max" /> was explicitly assigned. </summary>
    internal bool HasMax { get; private set; }
}
