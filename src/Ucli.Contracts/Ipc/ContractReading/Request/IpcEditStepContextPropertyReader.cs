using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads the mutually exclusive properties inside <c>step.on</c>. </summary>
internal static class IpcEditStepContextPropertyReader
{
    public static bool TryRead (
        JsonElement onElement,
        out IpcEditStepContract.EditContext context,
        out string errorMessage)
    {
        var state = new ContextReadState();
        foreach (var property in onElement.EnumerateObject())
        {
            if (!TryReadProperty(property, state, out errorMessage))
            {
                context = default!;
                return false;
            }
        }

        return state.TryCreateContext(out context, out errorMessage);
    }

    private static bool TryReadProperty (
        JsonProperty property,
        ContextReadState state,
        out string errorMessage)
    {
        return property.Name switch
        {
            "scene" => state.TryReadString(property, IpcEditStepContract.ContextKind.Scene, "step.on.scene", out errorMessage),
            "prefab" => state.TryReadString(property, IpcEditStepContract.ContextKind.Prefab, "step.on.prefab", out errorMessage),
            "asset" => state.TryReadString(property, IpcEditStepContract.ContextKind.Asset, "step.on.asset", out errorMessage),
            "project" => state.TryReadProject(property, out errorMessage),
            _ => UnknownProperty(property, out errorMessage),
        };
    }

    private static bool UnknownProperty (
        JsonProperty property,
        out string errorMessage)
    {
        errorMessage = $"Edit step property 'step.on' contains an unknown property: {property.Name}.";
        return false;
    }

    private sealed class ContextReadState
    {
        private bool hasScene;
        private bool hasPrefab;
        private bool hasAsset;
        private bool hasProject;
        private string? path;

        public bool TryReadString (
            JsonProperty property,
            IpcEditStepContract.ContextKind contextKind,
            string propertyPath,
            out string errorMessage)
        {
            var hasProperty = HasContext(contextKind);
            if (!IpcEditStepContractReadHelpers.TryReadUniqueString(property, propertyPath, ref hasProperty, out path, out errorMessage))
            {
                return false;
            }

            SetContext(contextKind, hasProperty);
            return true;
        }

        public bool TryReadProject (
            JsonProperty property,
            out string errorMessage)
        {
            if (hasProject)
            {
                errorMessage = "Edit step property 'step.on.project' is duplicated.";
                return false;
            }

            return TryApplyProject(property, out errorMessage);
        }

        public bool TryCreateContext (
            out IpcEditStepContract.EditContext context,
            out string errorMessage)
        {
            if (IpcEditStepContractReadHelpers.CountTrue(hasScene, hasPrefab, hasAsset, hasProject) != 1)
            {
                context = default!;
                errorMessage = "Edit step property 'step.on' must specify exactly one of 'scene', 'prefab', 'asset', or 'project'.";
                return false;
            }

            context = CreateContext();
            errorMessage = string.Empty;
            return true;
        }

        private bool TryApplyProject (
            JsonProperty property,
            out string errorMessage)
        {
            if (property.Value.ValueKind != JsonValueKind.True)
            {
                errorMessage = "Edit step property 'step.on.project' must be true.";
                return false;
            }

            hasProject = true;
            errorMessage = string.Empty;
            return true;
        }

        private bool HasContext (IpcEditStepContract.ContextKind contextKind)
        {
            return contextKind switch
            {
                IpcEditStepContract.ContextKind.Scene => hasScene,
                IpcEditStepContract.ContextKind.Prefab => hasPrefab,
                IpcEditStepContract.ContextKind.Asset => hasAsset,
                _ => hasProject,
            };
        }

        private void SetContext (
            IpcEditStepContract.ContextKind contextKind,
            bool hasProperty)
        {
            switch (contextKind)
            {
                case IpcEditStepContract.ContextKind.Scene:
                    hasScene = hasProperty;
                    return;
                case IpcEditStepContract.ContextKind.Prefab:
                    hasPrefab = hasProperty;
                    return;
                case IpcEditStepContract.ContextKind.Asset:
                    hasAsset = hasProperty;
                    return;
                default:
                    hasProject = hasProperty;
                    return;
            }
        }

        private IpcEditStepContract.EditContext CreateContext ()
        {
            if (hasScene)
            {
                return new IpcEditStepContract.EditContext(IpcEditStepContract.ContextKind.Scene, path);
            }

            if (hasPrefab)
            {
                return new IpcEditStepContract.EditContext(IpcEditStepContract.ContextKind.Prefab, path);
            }

            return hasAsset
                ? new IpcEditStepContract.EditContext(IpcEditStepContract.ContextKind.Asset, path)
                : new IpcEditStepContract.EditContext(IpcEditStepContract.ContextKind.Project, null);
        }
    }
}
