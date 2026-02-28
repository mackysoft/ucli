namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines touched-resource kind literal values used by <c>execute</c> response payload contracts. </summary>
public static class IpcExecuteTouchedResourceKindNames
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