namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines canonical primitive operation names shared by request contracts, CLI workflows, and Unity execution metadata. </summary>
public static class UcliPrimitiveOperationNames
{
    /// <summary> Gets the primitive operation name for <c>ucli.resolve</c>. </summary>
    public const string Resolve = "ucli.resolve";

    /// <summary> Gets the primitive operation name for <c>ucli.asset.create</c>. </summary>
    public const string AssetCreate = "ucli.asset.create";

    /// <summary> Gets the primitive operation name for <c>ucli.asset.schema</c>. </summary>
    public const string AssetSchema = "ucli.asset.schema";

    /// <summary> Gets the primitive operation name for <c>ucli.asset.set</c>. </summary>
    public const string AssetSet = "ucli.asset.set";

    /// <summary> Gets the primitive operation name for <c>ucli.assets.find</c>. </summary>
    public const string AssetsFind = "ucli.assets.find";

    /// <summary> Gets the primitive operation name for <c>ucli.cs.eval</c>. </summary>
    public const string CsEval = "ucli.cs.eval";

    /// <summary> Gets the primitive operation name for <c>ucli.comp.ensure</c>. </summary>
    public const string CompEnsure = "ucli.comp.ensure";

    /// <summary> Gets the primitive operation name for <c>ucli.comp.schema</c>. </summary>
    public const string CompSchema = "ucli.comp.schema";

    /// <summary> Gets the primitive operation name for <c>ucli.comp.set</c>. </summary>
    public const string CompSet = "ucli.comp.set";

    /// <summary> Gets the primitive operation name for <c>ucli.go.create</c>. </summary>
    public const string GoCreate = "ucli.go.create";

    /// <summary> Gets the primitive operation name for <c>ucli.go.delete</c>. </summary>
    public const string GoDelete = "ucli.go.delete";

    /// <summary> Gets the primitive operation name for <c>ucli.go.describe</c>. </summary>
    public const string GoDescribe = "ucli.go.describe";

    /// <summary> Gets the primitive operation name for <c>ucli.go.reparent</c>. </summary>
    public const string GoReparent = "ucli.go.reparent";

    /// <summary> Gets the primitive operation name for <c>ucli.prefab.create</c>. </summary>
    public const string PrefabCreate = "ucli.prefab.create";

    /// <summary> Gets the primitive operation name for <c>ucli.prefab.open</c>. </summary>
    public const string PrefabOpen = "ucli.prefab.open";

    /// <summary> Gets the primitive operation name for <c>ucli.prefab.save</c>. </summary>
    public const string PrefabSave = "ucli.prefab.save";

    /// <summary> Gets the primitive operation name for <c>ucli.project.refresh</c>. </summary>
    public const string ProjectRefresh = "ucli.project.refresh";

    /// <summary> Gets the primitive operation name for <c>ucli.project.save</c>. </summary>
    public const string ProjectSave = "ucli.project.save";

    /// <summary> Gets the primitive operation name for <c>ucli.scene.open</c>. </summary>
    public const string SceneOpen = "ucli.scene.open";

    /// <summary> Gets the primitive operation name for <c>ucli.scene.query</c>. </summary>
    public const string SceneQuery = "ucli.scene.query";

    /// <summary> Gets the primitive operation name for <c>ucli.scene.save</c>. </summary>
    public const string SceneSave = "ucli.scene.save";

    /// <summary> Gets the primitive operation name for <c>ucli.scene.tree</c>. </summary>
    public const string SceneTree = "ucli.scene.tree";
}
