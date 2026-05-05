using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Represents one parsed <c>kind:"edit"</c> step contract. </summary>
/// <param name="Id"> The normalized step identifier. </param>
/// <param name="Context"> The edit context. </param>
/// <param name="Selection"> The edit selection contract. </param>
/// <param name="Actions"> The ordered edit action list. </param>
/// <param name="Commit"> The save-boundary mode. </param>
internal sealed record IpcEditStepContract (
    string Id,
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
    internal enum CardinalityKind
    {
        One = 0,

        First,

        All,

        AtMostOne,
    }

    /// <summary>
    /// Defines the supported public edit actions.
    /// </summary>
    internal enum ActionKind
    {
        Set = 0,

        EnsureComponent,

        CreateObject,

        CreateAsset,

        CreatePrefab,

        Delete,

        Reparent,
    }

    /// <summary>
    /// Defines which save boundary the compiler appends after the edit action chain.
    /// </summary>
    internal enum CommitKind
    {
        None = 0,

        Context,

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
    /// <param name="Values"> The cloned assignment object for <c>set</c>. </param>
    internal sealed record EditAction (
        ActionKind Kind,
        string? Target,
        string? Alias,
        string? Type,
        string? Name,
        string? Path,
        string? Parent,
        JsonElement Values);
}
