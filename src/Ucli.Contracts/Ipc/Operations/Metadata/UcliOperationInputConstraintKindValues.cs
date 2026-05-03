namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines supported operation input constraint kind literals. </summary>
public static class UcliOperationInputConstraintKindValues
{
    /// <summary> Gets the non-empty value constraint. </summary>
    public const string NonEmpty = "nonEmpty";

    /// <summary> Gets the inclusive numeric range constraint. </summary>
    public const string Range = "range";

    /// <summary> Gets the project-relative path constraint. </summary>
    public const string ProjectRelativePath = "projectRelativePath";

    /// <summary> Gets the existing asset constraint. </summary>
    public const string AssetExists = "assetExists";

    /// <summary> Gets the creatable asset constraint. </summary>
    public const string AssetCreatable = "assetCreatable";

    /// <summary> Gets the Unity GlobalObjectId constraint. </summary>
    public const string GlobalObjectId = "globalObjectId";

    /// <summary> Gets the Unity hierarchy path constraint. </summary>
    public const string HierarchyPath = "hierarchyPath";

    /// <summary> Gets the resolvable reference constraint. </summary>
    public const string ReferenceResolvable = "referenceResolvable";

    /// <summary> Gets the type existence constraint. </summary>
    public const string TypeExists = "typeExists";

    /// <summary> Gets the type assignability constraint. </summary>
    public const string TypeAssignableTo = "typeAssignableTo";

    /// <summary> Gets the serialized property constraint. </summary>
    public const string SerializedProperty = "serializedProperty";

    /// <summary> Gets the Unity asset GUID constraint. </summary>
    public const string AssetGuid = "assetGuid";
}
