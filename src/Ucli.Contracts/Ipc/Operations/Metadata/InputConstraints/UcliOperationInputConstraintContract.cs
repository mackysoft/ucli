using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Describes one machine-readable semantic constraint for an operation input. </summary>
public sealed class UcliOperationInputConstraintContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliOperationInputConstraintContract" /> class. </summary>
    public UcliOperationInputConstraintContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationInputConstraintContract" /> class. </summary>
    /// <param name="kind"> The constraint kind literal. </param>
    public UcliOperationInputConstraintContract (string? kind)
    {
        Kind = kind;
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationInputConstraintContract" /> class. </summary>
    /// <param name="kind"> The constraint kind enum value. </param>
    public UcliOperationInputConstraintContract (UcliOperationInputConstraintKind kind)
        : this(UcliOperationInputConstraintKindCodec.ToValue(kind))
    {
    }

    /// <summary> Creates a constraint that rejects empty strings, arrays, or objects. </summary>
    /// <returns> The constraint contract. </returns>
    public static UcliOperationInputConstraintContract NonEmpty ()
    {
        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKind.NonEmpty);
    }

    /// <summary> Creates an inclusive numeric range constraint. </summary>
    /// <param name="min"> The inclusive minimum value, or <see langword="null" /> when unbounded. </param>
    /// <param name="max"> The inclusive maximum value, or <see langword="null" /> when unbounded. </param>
    /// <returns> The constraint contract. </returns>
    public static UcliOperationInputConstraintContract Range (
        double? min,
        double? max)
    {
        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKind.Range)
        {
            Min = min,
            Max = max,
        };
    }

    /// <summary> Creates a project-relative path constraint. </summary>
    /// <returns> The constraint contract. </returns>
    public static UcliOperationInputConstraintContract ProjectRelativePath ()
    {
        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKind.ProjectRelativePath);
    }

    /// <summary> Creates an existing-asset constraint. </summary>
    /// <param name="assetKind"> The asset kind enum value. </param>
    /// <returns> The constraint contract. </returns>
    public static UcliOperationInputConstraintContract AssetExists (UcliOperationAssetKind assetKind)
    {
        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKind.AssetExists)
        {
            AssetKind = UcliOperationAssetKindCodec.ToValue(assetKind),
        };
    }

    /// <summary> Creates an asset-creatable constraint. </summary>
    /// <param name="assetKind"> The asset kind enum value. </param>
    /// <returns> The constraint contract. </returns>
    public static UcliOperationInputConstraintContract AssetCreatable (UcliOperationAssetKind assetKind)
    {
        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKind.AssetCreatable)
        {
            AssetKind = UcliOperationAssetKindCodec.ToValue(assetKind),
        };
    }

    /// <summary> Creates a Unity GlobalObjectId syntax constraint. </summary>
    /// <returns> The constraint contract. </returns>
    public static UcliOperationInputConstraintContract GlobalObjectId ()
    {
        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKind.GlobalObjectId);
    }

    /// <summary> Creates a Unity asset GUID syntax constraint. </summary>
    /// <returns> The constraint contract. </returns>
    public static UcliOperationInputConstraintContract AssetGuid ()
    {
        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKind.AssetGuid);
    }

    /// <summary> Creates a Unity hierarchy path constraint. </summary>
    /// <returns> The constraint contract. </returns>
    public static UcliOperationInputConstraintContract HierarchyPath ()
    {
        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKind.HierarchyPath);
    }

    /// <summary> Creates a reference-resolvable constraint. </summary>
    /// <param name="targetKind"> The reference target kind enum value. </param>
    /// <returns> The constraint contract. </returns>
    public static UcliOperationInputConstraintContract ReferenceResolvable (UcliOperationReferenceTargetKind targetKind)
    {
        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKind.ReferenceResolvable)
        {
            TargetKind = UcliOperationReferenceTargetKindCodec.ToValue(targetKind),
        };
    }

    /// <summary> Creates a type-exists constraint. </summary>
    /// <returns> The constraint contract. </returns>
    public static UcliOperationInputConstraintContract TypeExists ()
    {
        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKind.TypeExists);
    }

    /// <summary> Creates a type-assignable constraint. </summary>
    /// <param name="typeKind"> The type kind enum value. </param>
    /// <returns> The constraint contract. </returns>
    public static UcliOperationInputConstraintContract TypeAssignableTo (UcliOperationTypeKind typeKind)
    {
        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKind.TypeAssignableTo)
        {
            TypeKind = UcliOperationTypeKindCodec.ToValue(typeKind),
        };
    }

    /// <summary> Creates a serialized-property access constraint. </summary>
    /// <param name="access"> The access enum value. </param>
    /// <returns> The constraint contract. </returns>
    public static UcliOperationInputConstraintContract SerializedProperty (UcliOperationSerializedPropertyAccess access)
    {
        return new UcliOperationInputConstraintContract(UcliOperationInputConstraintKind.SerializedProperty)
        {
            Access = UcliOperationSerializedPropertyAccessCodec.ToValue(access),
        };
    }

    /// <summary> Gets or sets the constraint kind literal. </summary>
    public string? Kind { get; set; }

    /// <summary> Gets or sets the asset kind parameter. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssetKind { get; set; }

    /// <summary> Gets or sets the reference target kind parameter. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetKind { get; set; }

    /// <summary> Gets or sets the type kind parameter. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TypeKind { get; set; }

    /// <summary> Gets or sets the serialized property access parameter. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Access { get; set; }

    /// <summary> Gets or sets the inclusive minimum range value. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Min { get; set; }

    /// <summary> Gets or sets the inclusive maximum range value. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Max { get; set; }
}
