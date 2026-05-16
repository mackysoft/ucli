namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines supported operation input constraint kinds. </summary>
public enum UcliOperationInputConstraintKind
{
    /// <summary> Rejects empty strings, arrays, or objects. </summary>
    NonEmpty = 0,

    /// <summary> Applies an inclusive numeric range. </summary>
    Range = 1,

    /// <summary> Requires a project-relative path. </summary>
    ProjectRelativePath = 2,

    /// <summary> Requires an existing Unity asset. </summary>
    AssetExists = 3,

    /// <summary> Requires a Unity asset path that can be created. </summary>
    AssetCreatable = 4,

    /// <summary> Requires Unity GlobalObjectId syntax. </summary>
    GlobalObjectId = 5,

    /// <summary> Requires a Unity hierarchy path. </summary>
    HierarchyPath = 6,

    /// <summary> Requires a reference resolvable to a target kind. </summary>
    ReferenceResolvable = 7,

    /// <summary> Requires a resolvable type. </summary>
    TypeExists = 8,

    /// <summary> Requires a type assignable to a target type kind. </summary>
    TypeAssignableTo = 9,

    /// <summary> Requires a serialized property access capability. </summary>
    SerializedProperty = 10,

    /// <summary> Requires Unity asset GUID syntax. </summary>
    AssetGuid = 11,

    /// <summary> Requires a bounded query window cursor. </summary>
    Cursor = 12,
}
