using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Validates public edit action shape rules after property-level reading. </summary>
internal static class IpcEditStepActionShapeValidator
{
    private static readonly IpcEditStepActionShapeDescriptor[] Descriptors =
    {
        Define(IpcEditStepContract.ActionKind.Set, ["kind", "target", "values"], [], null, requiresValues: true, requiresNonEmptyValues: true),
        Define(IpcEditStepContract.ActionKind.EnsureComponent, ["kind", "target", "type", "as"], ["type"], "Edit step property 'step.actions[{0}].type' is required."),
        Define(IpcEditStepContract.ActionKind.CreateObject, ["kind", "name", "as"], ["name"], "Edit step property 'step.actions[{0}].name' is required."),
        Define(IpcEditStepContract.ActionKind.CreateAsset, ["kind", "type", "path"], ["type", "path"], "Edit step action 'createAsset' requires both 'type' and 'path'."),
        Define(IpcEditStepContract.ActionKind.CreatePrefab, ["kind", "target", "path"], ["path"], "Edit step action 'createPrefab' requires 'path'."),
        Define(IpcEditStepContract.ActionKind.ApplyPrefabOverrides, ["kind", "target", "targetAssetPath", "propertyPaths"], ["targetAssetPath"], "Edit step action 'applyPrefabOverrides' requires 'targetAssetPath'."),
        Define(IpcEditStepContract.ActionKind.RevertPrefabOverrides, ["kind", "target", "targetAssetPath", "propertyPaths"], ["targetAssetPath"], "Edit step action 'revertPrefabOverrides' requires 'targetAssetPath'."),
        Define(IpcEditStepContract.ActionKind.Delete, ["kind", "target"], [], null),
        Define(IpcEditStepContract.ActionKind.Reparent, ["kind", "target", "parent"], ["parent"], "Edit step property 'step.actions[{0}].parent' is required."),
    };

    public static bool TryValidate (
        JsonElement actionElement,
        int actionIndex,
        in IpcEditStepActionReadState state,
        out string errorMessage)
    {
        if (!TryResolveDescriptor(state.ActionKind, out var descriptor))
        {
            errorMessage = $"Unsupported edit action kind '{state.ActionKind}'.";
            return false;
        }

        return TryValidateAllowedProperties(actionElement, actionIndex, descriptor, out errorMessage)
            && TryValidateValues(actionIndex, descriptor, state, out errorMessage)
            && TryValidateRequiredStrings(actionIndex, descriptor, state, out errorMessage);
    }

    private static IpcEditStepActionShapeDescriptor Define (
        IpcEditStepContract.ActionKind kind,
        string[] allowedProperties,
        string[] requiredStringProperties,
        string? missingRequiredMessage,
        bool requiresValues = false,
        bool requiresNonEmptyValues = false)
    {
        return new IpcEditStepActionShapeDescriptor(
            kind,
            new HashSet<string>(allowedProperties, StringComparer.Ordinal),
            requiredStringProperties,
            missingRequiredMessage,
            requiresValues,
            requiresNonEmptyValues);
    }

    private static bool TryResolveDescriptor (
        IpcEditStepContract.ActionKind actionKind,
        out IpcEditStepActionShapeDescriptor descriptor)
    {
        for (var i = 0; i < Descriptors.Length; i++)
        {
            if (Descriptors[i].Kind == actionKind)
            {
                descriptor = Descriptors[i];
                return true;
            }
        }

        descriptor = default!;
        return false;
    }

    private static bool TryValidateAllowedProperties (
        JsonElement actionElement,
        int actionIndex,
        IpcEditStepActionShapeDescriptor descriptor,
        out string errorMessage)
    {
        foreach (var property in actionElement.EnumerateObject())
        {
            if (!descriptor.AllowedProperties.Contains(property.Name))
            {
                errorMessage = $"Edit step property 'step.actions[{actionIndex}]' contains an unknown property: {property.Name}.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateValues (
        int actionIndex,
        IpcEditStepActionShapeDescriptor descriptor,
        in IpcEditStepActionReadState state,
        out string errorMessage)
    {
        if (descriptor.RequiresValues && !state.HasValues)
        {
            errorMessage = $"Edit step property 'step.actions[{actionIndex}].values' is required.";
            return false;
        }

        if (descriptor.RequiresNonEmptyValues && !HasAtLeastOneProperty(state.Values))
        {
            errorMessage = $"Edit step property 'step.actions[{actionIndex}].values' must contain at least one assignment.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateRequiredStrings (
        int actionIndex,
        IpcEditStepActionShapeDescriptor descriptor,
        in IpcEditStepActionReadState state,
        out string errorMessage)
    {
        for (var i = 0; i < descriptor.RequiredStringProperties.Count; i++)
        {
            if (!state.HasRequiredString(descriptor.RequiredStringProperties[i]))
            {
                errorMessage = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    descriptor.MissingRequiredMessage!,
                    actionIndex);
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool HasAtLeastOneProperty (JsonElement element)
    {
        var enumerator = element.EnumerateObject();
        return enumerator.MoveNext();
    }
}
