using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads direct edit selections declared under <c>step.select</c>. </summary>
internal static class IpcEditStepDirectSelectionReader
{
    public static bool TryRead (
        JsonElement selectElement,
        IpcEditStepContract.ContextKind contextKind,
        IpcEditStepContract.CardinalityKind cardinality,
        out IpcEditStepContract.EditSelection selection,
        out string errorMessage)
    {
        selection = default!;
        if (!TryValidateCardinality(cardinality, out errorMessage))
        {
            return false;
        }

        var state = new DirectSelectionState();
        if (!TryReadProperties(selectElement, state, out errorMessage)
            || !state.TryValidateContext(contextKind, out errorMessage))
        {
            return false;
        }

        selection = state.CreateSelection(cardinality);
        return true;
    }

    private static bool TryValidateCardinality (
        IpcEditStepContract.CardinalityKind cardinality,
        out string errorMessage)
    {
        if (cardinality == IpcEditStepContract.CardinalityKind.First)
        {
            errorMessage = "Edit step property 'step.select.cardinality' value 'first' is supported only for candidate-source selections.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryReadProperties (
        JsonElement selectElement,
        DirectSelectionState state,
        out string errorMessage)
    {
        foreach (var property in selectElement.EnumerateObject())
        {
            if (!TryReadProperty(property, state, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryReadProperty (
        JsonProperty property,
        DirectSelectionState state,
        out string errorMessage)
    {
        return property.Name switch
        {
            "cardinality" => SkipProperty(out errorMessage),
            "gameObject" => state.TryReadGameObject(property, out errorMessage),
            "component" => state.TryReadComponent(property, out errorMessage),
            "self" => state.TryReadSelf(property, out errorMessage),
            "projectAsset" => state.TryReadProjectAsset(property, out errorMessage),
            _ => UnknownProperty(property, out errorMessage),
        };
    }

    private static bool SkipProperty (out string errorMessage)
    {
        errorMessage = string.Empty;
        return true;
    }

    private static bool UnknownProperty (
        JsonProperty property,
        out string errorMessage)
    {
        errorMessage = $"Edit step property 'step.select' contains an unknown property: {property.Name}.";
        return false;
    }

    private sealed class DirectSelectionState
    {
        private string? gameObjectPath;
        private string? componentType;
        private bool self;
        private string? projectAssetPath;
        private bool hasGameObject;
        private bool hasComponent;
        private bool hasSelf;
        private bool hasProjectAsset;

        public bool TryReadGameObject (JsonProperty property, out string errorMessage)
        {
            return IpcEditStepContractReadHelpers.TryReadUniqueString(
                property,
                "step.select.gameObject",
                ref hasGameObject,
                out gameObjectPath,
                out errorMessage);
        }

        public bool TryReadComponent (JsonProperty property, out string errorMessage)
        {
            return IpcEditStepContractReadHelpers.TryReadUniqueString(
                property,
                "step.select.component",
                ref hasComponent,
                out componentType,
                out errorMessage);
        }

        public bool TryReadSelf (JsonProperty property, out string errorMessage)
        {
            if (hasSelf)
            {
                errorMessage = "Edit step property 'step.select.self' is duplicated.";
                return false;
            }

            return TryApplySelf(property, out errorMessage);
        }

        public bool TryReadProjectAsset (JsonProperty property, out string errorMessage)
        {
            if (hasProjectAsset)
            {
                errorMessage = "Edit step property 'step.select.projectAsset' is duplicated.";
                return false;
            }

            return TryApplyProjectAsset(property, out errorMessage);
        }

        public bool TryValidateContext (
            IpcEditStepContract.ContextKind contextKind,
            out string errorMessage)
        {
            return contextKind switch
            {
                IpcEditStepContract.ContextKind.Scene => TryValidateSceneOrPrefab(out errorMessage),
                IpcEditStepContract.ContextKind.Prefab => TryValidateSceneOrPrefab(out errorMessage),
                IpcEditStepContract.ContextKind.Asset => TryValidateAsset(out errorMessage),
                _ => TryValidateProject(out errorMessage),
            };
        }

        public IpcEditStepContract.EditSelection CreateSelection (IpcEditStepContract.CardinalityKind cardinality)
        {
            return new IpcEditStepContract.EditSelection(
                IpcEditStepContract.SelectionKind.Direct,
                cardinality,
                gameObjectPath,
                componentType,
                self,
                projectAssetPath,
                SourceOperation: null,
                SourceArgs: default);
        }

        private bool TryApplySelf (JsonProperty property, out string errorMessage)
        {
            if (property.Value.ValueKind != JsonValueKind.True)
            {
                errorMessage = "Edit step property 'step.select.self' must be true when specified.";
                return false;
            }

            hasSelf = true;
            self = true;
            errorMessage = string.Empty;
            return true;
        }

        private bool TryApplyProjectAsset (JsonProperty property, out string errorMessage)
        {
            if (!IpcEditStepProjectAssetSelectorReader.TryReadPath(property.Value, out projectAssetPath, out errorMessage))
            {
                return false;
            }

            hasProjectAsset = true;
            return true;
        }

        private bool TryValidateSceneOrPrefab (out string errorMessage)
        {
            if (!hasGameObject)
            {
                errorMessage = "Edit step property 'step.select.gameObject' is required for scene/prefab context.";
                return false;
            }

            return RejectInvalidSceneOrPrefabProperties(out errorMessage);
        }

        private bool RejectInvalidSceneOrPrefabProperties (out string errorMessage)
        {
            if (hasSelf || hasProjectAsset)
            {
                errorMessage = "Edit step direct selector for scene/prefab context can only use 'gameObject' and optional 'component'.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private bool TryValidateAsset (out string errorMessage)
        {
            if (!hasSelf || hasGameObject || hasComponent || hasProjectAsset)
            {
                errorMessage = "Edit step direct selector for asset context must use only 'self: true'.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private bool TryValidateProject (out string errorMessage)
        {
            if (!hasProjectAsset || hasGameObject || hasComponent || hasSelf)
            {
                errorMessage = "Edit step direct selector for project context must use only 'projectAsset.path'.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
