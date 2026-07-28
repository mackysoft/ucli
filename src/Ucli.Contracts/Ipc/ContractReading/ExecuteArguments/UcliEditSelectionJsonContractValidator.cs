namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary>Validates edit context and selection DTOs, including their product-specific compatibility.</summary>
internal static class UcliEditSelectionJsonContractValidator
{
    public static bool TryValidateContext (
        UcliEditContextJsonContract context,
        string path,
        out IpcEditStepContract.ContextKind kind,
        out string errorMessage)
    {
        return context switch
        {
            UcliSceneEditContextJsonContract scene => TryValidateContextPath(
                scene.Path, IpcEditStepContract.ContextKind.Scene, path, out kind, out errorMessage),
            UcliPrefabEditContextJsonContract prefab => TryValidateContextPath(
                prefab.Path, IpcEditStepContract.ContextKind.Prefab, path, out kind, out errorMessage),
            UcliAssetEditContextJsonContract asset => TryValidateContextPath(
                asset.Path, IpcEditStepContract.ContextKind.Asset, path, out kind, out errorMessage),
            UcliProjectEditContextJsonContract => ValidProjectContext(out kind, out errorMessage),
            _ => UnsupportedContext(path, out kind, out errorMessage),
        };
    }

    public static bool TryValidateSelection (
        UcliEditSelectionJsonContract selection,
        IpcEditStepContract.ContextKind contextKind,
        string path,
        out string errorMessage)
    {
        if (!TryValidateCardinality(selection, path, out errorMessage))
        {
            return false;
        }

        return contextKind switch
        {
            IpcEditStepContract.ContextKind.Scene or IpcEditStepContract.ContextKind.Prefab =>
                TryValidateHierarchySelection(selection, contextKind, path, out errorMessage),
            IpcEditStepContract.ContextKind.Asset or IpcEditStepContract.ContextKind.Project =>
                TryValidateNonHierarchySelection(selection, contextKind, path, out errorMessage),
            _ => Unsupported(path, out errorMessage),
        };
    }

    private static bool TryValidateHierarchySelection (
        UcliEditSelectionJsonContract selection,
        IpcEditStepContract.ContextKind contextKind,
        string path,
        out string errorMessage)
    {
        return contextKind == IpcEditStepContract.ContextKind.Scene
            ? TryValidateSceneSelection(selection, path, out errorMessage)
            : TryValidatePrefabSelection(selection, path, out errorMessage);
    }

    private static bool TryValidateNonHierarchySelection (
        UcliEditSelectionJsonContract selection,
        IpcEditStepContract.ContextKind contextKind,
        string path,
        out string errorMessage)
    {
        return contextKind == IpcEditStepContract.ContextKind.Asset
            ? TryValidateAssetSelection(selection, path, out errorMessage)
            : TryValidateProjectSelection(selection, path, out errorMessage);
    }

    private static bool TryValidateCardinality (
        UcliEditSelectionJsonContract selection,
        string path,
        out string errorMessage)
    {
        if (selection is not UcliFromEditSelectionJsonContract
            && selection.Cardinality == IpcEditStepContract.CardinalityKind.First)
        {
            errorMessage = $"Request property '{path}/cardinality' value 'first' requires a candidate source.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateContextPath (
        string contextPath,
        IpcEditStepContract.ContextKind contextKind,
        string path,
        out IpcEditStepContract.ContextKind kind,
        out string errorMessage)
    {
        kind = contextKind;
        return UcliRequestJsonTextContractValidator.TryValidate(
            contextPath,
            $"{path}/path",
            out errorMessage);
    }

    private static bool ValidProjectContext (
        out IpcEditStepContract.ContextKind kind,
        out string errorMessage)
    {
        kind = IpcEditStepContract.ContextKind.Project;
        errorMessage = string.Empty;
        return true;
    }

    private static bool UnsupportedContext (
        string path,
        out IpcEditStepContract.ContextKind kind,
        out string errorMessage)
    {
        kind = default;
        errorMessage = $"Request property '{path}/kind' is unsupported.";
        return false;
    }

    private static bool TryValidateSceneSelection (
        UcliEditSelectionJsonContract selection,
        string path,
        out string errorMessage)
    {
        return selection switch
        {
            UcliFromEditSelectionJsonContract from => TryValidateFrom(from, path, out errorMessage),
            UcliGameObjectEditSelectionJsonContract gameObject =>
                TryValidateGameObject(gameObject, path, out errorMessage),
            _ => Unsupported(path, out errorMessage),
        };
    }

    private static bool TryValidatePrefabSelection (
        UcliEditSelectionJsonContract selection,
        string path,
        out string errorMessage)
    {
        return selection is UcliGameObjectEditSelectionJsonContract gameObject
            ? TryValidateGameObject(gameObject, path, out errorMessage)
            : Unsupported(path, out errorMessage);
    }

    private static bool TryValidateAssetSelection (
        UcliEditSelectionJsonContract selection,
        string path,
        out string errorMessage)
    {
        if (selection is UcliSelfEditSelectionJsonContract)
        {
            errorMessage = string.Empty;
            return true;
        }

        return Unsupported(path, out errorMessage);
    }

    private static bool TryValidateProjectSelection (
        UcliEditSelectionJsonContract selection,
        string path,
        out string errorMessage)
    {
        return selection is UcliProjectAssetEditSelectionJsonContract projectAsset
            ? UcliRequestJsonTextContractValidator.TryValidate(
                projectAsset.Path,
                $"{path}/path",
                out errorMessage)
            : Unsupported(path, out errorMessage);
    }

    private static bool TryValidateGameObject (
        UcliGameObjectEditSelectionJsonContract gameObject,
        string path,
        out string errorMessage)
    {
        return UcliRequestJsonTextContractValidator.TryValidate(
                   gameObject.Path,
                   $"{path}/path",
                   out errorMessage)
               && UcliRequestJsonTextContractValidator.TryValidateOptional(
                   gameObject.Component,
                   $"{path}/component",
                   out errorMessage);
    }

    private static bool TryValidateFrom (
        UcliFromEditSelectionJsonContract from,
        string path,
        out string errorMessage)
    {
        if (from.Args == null)
        {
            errorMessage = $"Request property '{path}/args' is required.";
            return false;
        }

        return UcliRequestJsonTextContractValidator.TryValidateOptional(
                   from.Args.PathPrefix,
                   $"{path}/args/pathPrefix",
                   out errorMessage)
               && UcliRequestJsonTextContractValidator.TryValidateOptional(
                   from.Args.ComponentType,
                   $"{path}/args/componentType",
                   out errorMessage);
    }

    private static bool Unsupported (string path, out string errorMessage)
    {
        errorMessage = $"Request property '{path}' is not supported by the selected edit context.";
        return false;
    }
}
