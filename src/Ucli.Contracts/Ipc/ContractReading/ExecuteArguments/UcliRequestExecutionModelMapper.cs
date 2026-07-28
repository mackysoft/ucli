using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary>Maps validated request DTOs to the internal execution model.</summary>
internal static class UcliRequestExecutionModelMapper
{
    public static IpcEditStepContract MapEdit (
        UcliEditRequestStepJsonContract source,
        IpcExecuteStepId stepId)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (stepId == null)
        {
            throw new ArgumentNullException(nameof(stepId));
        }

        return new IpcEditStepContract(
            stepId,
            MapContext(source.On),
            MapSelection(source.Select),
            source.Actions.Select(MapAction).ToArray(),
            source.Commit);
    }

    private static IpcEditStepContract.EditContext MapContext (UcliEditContextJsonContract context)
    {
        return context switch
        {
            UcliSceneEditContextJsonContract scene => new IpcEditStepContract.EditContext(
                IpcEditStepContract.ContextKind.Scene,
                scene.Path),
            UcliPrefabEditContextJsonContract prefab => new IpcEditStepContract.EditContext(
                IpcEditStepContract.ContextKind.Prefab,
                prefab.Path),
            UcliAssetEditContextJsonContract asset => new IpcEditStepContract.EditContext(
                IpcEditStepContract.ContextKind.Asset,
                asset.Path),
            UcliProjectEditContextJsonContract => new IpcEditStepContract.EditContext(
                IpcEditStepContract.ContextKind.Project,
                null),
            _ => throw new InvalidOperationException($"Unsupported edit context contract: {context.GetType().FullName}."),
        };
    }

    private static IpcEditStepContract.EditSelection MapSelection (UcliEditSelectionJsonContract selection)
    {
        return selection switch
        {
            UcliFromEditSelectionJsonContract from => MapFromSelection(from),
            UcliGameObjectEditSelectionJsonContract gameObject => MapGameObjectSelection(gameObject),
            UcliSelfEditSelectionJsonContract self => MapSelfSelection(self),
            UcliProjectAssetEditSelectionJsonContract projectAsset => MapProjectAssetSelection(projectAsset),
            _ => throw new InvalidOperationException($"Unsupported edit selection contract: {selection.GetType().FullName}."),
        };
    }

    private static IpcEditStepContract.EditSelection MapFromSelection (
        UcliFromEditSelectionJsonContract source)
    {
        return new IpcEditStepContract.EditSelection(
            IpcEditStepContract.SelectionKind.From,
            source.Cardinality,
            GameObjectPath: null,
            ComponentType: null,
            Self: false,
            ProjectAssetPath: null,
            SourceOperation: Vocabulary.GetText(source.Op),
            SourcePathPrefix: source.Args.PathPrefix,
            SourceComponentType: source.Args.ComponentType);
    }

    private static IpcEditStepContract.EditSelection MapGameObjectSelection (
        UcliGameObjectEditSelectionJsonContract source)
    {
        return CreateDirectSelection(
            source.Cardinality,
            gameObjectPath: source.Path,
            componentType: source.Component);
    }

    private static IpcEditStepContract.EditSelection MapSelfSelection (
        UcliSelfEditSelectionJsonContract source)
    {
        return CreateDirectSelection(source.Cardinality, isSelf: true);
    }

    private static IpcEditStepContract.EditSelection MapProjectAssetSelection (
        UcliProjectAssetEditSelectionJsonContract source)
    {
        return CreateDirectSelection(source.Cardinality, projectAssetPath: source.Path);
    }

    private static IpcEditStepContract.EditSelection CreateDirectSelection (
        IpcEditStepContract.CardinalityKind cardinality,
        string? gameObjectPath = null,
        string? componentType = null,
        bool isSelf = false,
        string? projectAssetPath = null)
    {
        return new IpcEditStepContract.EditSelection(
            IpcEditStepContract.SelectionKind.Direct,
            cardinality,
            gameObjectPath,
            componentType,
            isSelf,
            projectAssetPath,
            SourceOperation: null,
            SourcePathPrefix: null,
            SourceComponentType: null);
    }

    private static IpcEditStepContract.EditAction MapAction (UcliEditActionJsonContract action)
    {
        return action switch
        {
            UcliSetEditActionJsonContract set => MapSetAction(set),
            UcliEnsureComponentEditActionJsonContract ensure => MapEnsureComponentAction(ensure),
            UcliPrefabOverridesEditActionJsonContract prefabOverrides => MapPrefabOverridesAction(prefabOverrides),
            UcliDeleteEditActionJsonContract delete => MapDeleteAction(delete),
            UcliReparentEditActionJsonContract reparent => MapReparentAction(reparent),
            _ => MapCreationAction(action),
        };
    }

    private static IpcEditStepContract.EditAction MapCreationAction (UcliEditActionJsonContract action)
    {
        return action switch
        {
            UcliCreateObjectEditActionJsonContract createObject => new IpcEditStepContract.EditAction(
                IpcEditStepContract.ActionKind.CreateObject,
                null, createObject.Alias, null, createObject.Name, null, null, null, null, default),
            UcliCreateAssetEditActionJsonContract createAsset => new IpcEditStepContract.EditAction(
                IpcEditStepContract.ActionKind.CreateAsset,
                null, null, createAsset.Type, null, createAsset.Path, null, null, null, default),
            UcliCreatePrefabEditActionJsonContract createPrefab => new IpcEditStepContract.EditAction(
                IpcEditStepContract.ActionKind.CreatePrefab,
                createPrefab.Target, null, null, null, createPrefab.Path, null, null, null, default),
            _ => throw new InvalidOperationException($"Unsupported edit action contract: {action.GetType().FullName}."),
        };
    }

    private static IpcEditStepContract.EditAction MapSetAction (UcliSetEditActionJsonContract action)
    {
        return new IpcEditStepContract.EditAction(
            IpcEditStepContract.ActionKind.Set,
            action.Target, null, null, null, null, null, null, null,
            JsonSerializer.SerializeToElement(action.Values, IpcJsonSerializerOptions.StrictPropertyNames));
    }

    private static IpcEditStepContract.EditAction MapEnsureComponentAction (
        UcliEnsureComponentEditActionJsonContract action)
    {
        return new IpcEditStepContract.EditAction(
            IpcEditStepContract.ActionKind.EnsureComponent,
            action.Target, action.Alias, action.Type, null, null, null, null, null, default);
    }

    private static IpcEditStepContract.EditAction MapPrefabOverridesAction (
        UcliPrefabOverridesEditActionJsonContract action)
    {
        var kind = action is UcliApplyPrefabOverridesEditActionJsonContract
            ? IpcEditStepContract.ActionKind.ApplyPrefabOverrides
            : IpcEditStepContract.ActionKind.RevertPrefabOverrides;
        return new IpcEditStepContract.EditAction(
            kind,
            action.Target, null, null, null, null, null, action.TargetAssetPath, action.PropertyPaths, default);
    }

    private static IpcEditStepContract.EditAction MapDeleteAction (
        UcliDeleteEditActionJsonContract action)
    {
        return new IpcEditStepContract.EditAction(
            IpcEditStepContract.ActionKind.Delete,
            action.Target, null, null, null, null, null, null, null, default);
    }

    private static IpcEditStepContract.EditAction MapReparentAction (
        UcliReparentEditActionJsonContract action)
    {
        return new IpcEditStepContract.EditAction(
            IpcEditStepContract.ActionKind.Reparent,
            action.Target, null, null, null, null, action.Parent, null, null, default);
    }
}
