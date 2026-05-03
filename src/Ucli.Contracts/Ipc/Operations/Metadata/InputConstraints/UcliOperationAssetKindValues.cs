namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines asset-kind literals used by operation input constraints. </summary>
public static class UcliOperationAssetKindValues
{
    /// <summary> Gets the value for a regular Unity asset. </summary>
    public const string Asset = "asset";

    /// <summary> Gets the value for a Unity prefab asset. </summary>
    public const string Prefab = "prefab";

    /// <summary> Gets the value for Unity project settings assets. </summary>
    public const string ProjectSettings = "projectSettings";

    /// <summary> Gets the value for a Unity scene asset. </summary>
    public const string Scene = "scene";
}
