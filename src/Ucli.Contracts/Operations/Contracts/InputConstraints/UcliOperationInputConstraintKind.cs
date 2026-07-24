
namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported operation input constraint kinds. </summary>
[VocabularyDefinition]
public enum UcliOperationInputConstraintKind
{
    /// <summary> Rejects empty strings, arrays, or objects. </summary>
    [VocabularyText("nonEmpty")]
    NonEmpty = 0,

    /// <summary> Applies an inclusive numeric range. </summary>
    [VocabularyText("range")]
    Range = 1,

    /// <summary> Requires a project-relative path. </summary>
    [VocabularyText("projectRelativePath")]
    ProjectRelativePath = 2,

    /// <summary> Requires an existing Unity asset. </summary>
    [VocabularyText("assetExists")]
    AssetExists = 3,

    /// <summary> Requires a Unity asset path that can be created. </summary>
    [VocabularyText("assetCreatable")]
    AssetCreatable = 4,

    /// <summary> Requires Unity GlobalObjectId syntax. </summary>
    [VocabularyText("globalObjectId")]
    GlobalObjectId = 5,

    /// <summary> Requires a Unity hierarchy path. </summary>
    [VocabularyText("hierarchyPath")]
    HierarchyPath = 6,

    /// <summary> Requires a reference resolvable to a target kind. </summary>
    [VocabularyText("referenceResolvable")]
    ReferenceResolvable = 7,

    /// <summary> Requires a resolvable type. </summary>
    [VocabularyText("typeExists")]
    TypeExists = 8,

    /// <summary> Requires a type assignable to a target type kind. </summary>
    [VocabularyText("typeAssignableTo")]
    TypeAssignableTo = 9,

    /// <summary> Requires a serialized property access capability. </summary>
    [VocabularyText("serializedProperty")]
    SerializedProperty = 10,

    /// <summary> Requires Unity asset GUID syntax. </summary>
    [VocabularyText("assetGuid")]
    AssetGuid = 11,

    /// <summary> Requires a bounded query window cursor. </summary>
    [VocabularyText("cursor")]
    Cursor = 12,
}
