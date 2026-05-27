using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported operation input constraint kinds. </summary>
public enum UcliOperationInputConstraintKind
{
    /// <summary> Rejects empty strings, arrays, or objects. </summary>
    [UcliContractLiteral("nonEmpty")]
    NonEmpty = 0,

    /// <summary> Applies an inclusive numeric range. </summary>
    [UcliContractLiteral("range")]
    Range = 1,

    /// <summary> Requires a project-relative path. </summary>
    [UcliContractLiteral("projectRelativePath")]
    ProjectRelativePath = 2,

    /// <summary> Requires an existing Unity asset. </summary>
    [UcliContractLiteral("assetExists")]
    AssetExists = 3,

    /// <summary> Requires a Unity asset path that can be created. </summary>
    [UcliContractLiteral("assetCreatable")]
    AssetCreatable = 4,

    /// <summary> Requires Unity GlobalObjectId syntax. </summary>
    [UcliContractLiteral("globalObjectId")]
    GlobalObjectId = 5,

    /// <summary> Requires a Unity hierarchy path. </summary>
    [UcliContractLiteral("hierarchyPath")]
    HierarchyPath = 6,

    /// <summary> Requires a reference resolvable to a target kind. </summary>
    [UcliContractLiteral("referenceResolvable")]
    ReferenceResolvable = 7,

    /// <summary> Requires a resolvable type. </summary>
    [UcliContractLiteral("typeExists")]
    TypeExists = 8,

    /// <summary> Requires a type assignable to a target type kind. </summary>
    [UcliContractLiteral("typeAssignableTo")]
    TypeAssignableTo = 9,

    /// <summary> Requires a serialized property access capability. </summary>
    [UcliContractLiteral("serializedProperty")]
    SerializedProperty = 10,

    /// <summary> Requires Unity asset GUID syntax. </summary>
    [UcliContractLiteral("assetGuid")]
    AssetGuid = 11,

    /// <summary> Requires a bounded query window cursor. </summary>
    [UcliContractLiteral("cursor")]
    Cursor = 12,
}
