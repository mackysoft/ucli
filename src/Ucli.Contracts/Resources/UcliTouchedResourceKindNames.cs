namespace MackySoft.Ucli.Contracts;

/// <summary> Defines touched-resource kind literal values shared by operation assurance metadata and touched-resource payloads. </summary>
public static class UcliTouchedResourceKindNames
{
    /// <summary> Gets the touched kind literal for scene resources. </summary>
    public const string Scene = "scene";

    /// <summary> Gets the touched kind literal for prefab resources. </summary>
    public const string Prefab = "prefab";

    /// <summary> Gets the touched kind literal for generic asset resources. </summary>
    public const string Asset = "asset";

    /// <summary> Gets the touched kind literal for project settings resources. </summary>
    public const string ProjectSettings = "projectSettings";
}
