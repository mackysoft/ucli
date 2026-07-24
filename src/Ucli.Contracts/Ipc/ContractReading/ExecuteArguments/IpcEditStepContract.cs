using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Represents one parsed <c>kind:"edit"</c> step contract. </summary>
/// <param name="Id"> The normalized step identifier. </param>
/// <param name="Context"> The edit context. </param>
/// <param name="Selection"> The edit selection contract. </param>
/// <param name="Actions"> The ordered edit action list. </param>
/// <param name="Commit"> The save-boundary mode. </param>
internal sealed record IpcEditStepContract (
    IpcExecuteStepId Id,
    IpcEditStepContract.EditContext Context,
    IpcEditStepContract.EditSelection Selection,
    IReadOnlyList<IpcEditStepContract.EditAction> Actions,
    IpcEditStepContract.CommitKind Commit)
{
    /// <summary>
    /// Defines the supported edit execution contexts.
    /// </summary>
    internal enum ContextKind
    {
        Scene = 0,

        Prefab,

        Asset,

        Project,
    }

    /// <summary>
    /// Defines the supported selection source forms for one edit step.
    /// </summary>
    internal enum SelectionKind
    {
        Direct = 0,

        From,
    }

    /// <summary>
    /// Defines how the normalized selection set must be reduced before action compilation.
    /// </summary>
    [VocabularyDefinition]
    internal enum CardinalityKind
    {
        [VocabularyText("one")]
        One = 0,

        [VocabularyText("first")]
        First,

        [VocabularyText("all")]
        All,

        [VocabularyText("atMostOne")]
        AtMostOne,
    }

    /// <summary>
    /// Defines the supported public edit actions.
    /// </summary>
    [VocabularyDefinition]
    internal enum ActionKind
    {
        [VocabularyText("set")]
        Set = 0,

        [VocabularyText("ensureComponent")]
        EnsureComponent,

        [VocabularyText("createObject")]
        CreateObject,

        [VocabularyText("createAsset")]
        CreateAsset,

        [VocabularyText("createPrefab")]
        CreatePrefab,

        [VocabularyText("applyPrefabOverrides")]
        ApplyPrefabOverrides,

        [VocabularyText("revertPrefabOverrides")]
        RevertPrefabOverrides,

        [VocabularyText("delete")]
        Delete,

        [VocabularyText("reparent")]
        Reparent,
    }

    /// <summary>
    /// Defines which save boundary the compiler appends after the edit action chain.
    /// </summary>
    [VocabularyDefinition]
    internal enum CommitKind
    {
        [VocabularyText("none")]
        None = 0,

        [VocabularyText("context")]
        Context,

        [VocabularyText("project")]
        Project,
    }

    /// <summary>
    /// Represents one validated edit context declaration.
    /// </summary>
    /// <param name="Kind"> The selected context kind. </param>
    /// <param name="Path"> The context path for scene, prefab, or asset contexts. <see langword="null" /> for project context. </param>
    internal sealed record EditContext (
        ContextKind Kind,
        string? Path);

    /// <summary>
    /// Represents one validated edit selection declaration before runtime target resolution.
    /// </summary>
    /// <param name="Kind"> The selection source form. </param>
    /// <param name="Cardinality"> The cardinality rule applied after selection normalization. </param>
    /// <param name="GameObjectPath"> The direct hierarchy path for scene or prefab selections. </param>
    /// <param name="ComponentType"> The optional component type constraint for direct or query-based component selections. </param>
    /// <param name="Self"> <see langword="true" /> when the asset context selects the context asset itself. </param>
    /// <param name="ProjectAssetPath"> The selected project asset path for project-scoped direct selection. </param>
    /// <param name="SourceOperation"> The selection-source operation name for <see cref="SelectionKind.From" /> selections. </param>
    /// <param name="SourceArgs"> The cloned source-operation argument object for <see cref="SelectionKind.From" /> selections. </param>
    internal sealed record EditSelection (
        SelectionKind Kind,
        CardinalityKind Cardinality,
        string? GameObjectPath,
        string? ComponentType,
        bool Self,
        string? ProjectAssetPath,
        string? SourceOperation,
        JsonElement SourceArgs);

    /// <summary>
    /// Represents one validated public edit action before lowering.
    /// </summary>
    /// <param name="Kind"> The action kind. </param>
    /// <param name="Target"> The optional public target literal. <see langword="null" /> uses the branch selection target. </param>
    /// <param name="Alias"> The optional public alias name created by this action. </param>
    /// <param name="Type"> The declared runtime type identifier for type-driven actions. </param>
    /// <param name="Name"> The created GameObject name for <c>createObject</c>. </param>
    /// <param name="Path"> The created asset or prefab path for path-producing actions. </param>
    /// <param name="Parent"> The public parent literal for <c>reparent</c>. </param>
    /// <param name="TargetAssetPath"> The explicit prefab asset path for prefab override actions. </param>
    /// <param name="PropertyPaths"> The optional exact SerializedProperty paths for prefab override actions. </param>
    /// <param name="Values"> The cloned assignment object for <c>set</c>. </param>
    internal sealed record EditAction (
        ActionKind Kind,
        string? Target,
        string? Alias,
        string? Type,
        string? Name,
        string? Path,
        string? Parent,
        string? TargetAssetPath,
        IReadOnlyList<string>? PropertyPaths,
        JsonElement Values);
}
