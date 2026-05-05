using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Reads the <c>actions</c> array for one public edit step. </summary>
internal static class IpcEditStepActionsReader
{
    private static readonly HashSet<string> SetActionProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "target",
        "values",
    };

    private static readonly HashSet<string> EnsureComponentActionProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "target",
        "type",
        "as",
    };

    private static readonly HashSet<string> CreateObjectActionProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "name",
        "as",
    };

    private static readonly HashSet<string> CreateAssetActionProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "type",
        "path",
    };

    private static readonly HashSet<string> CreatePrefabActionProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "target",
        "path",
    };

    private static readonly HashSet<string> DeleteActionProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "target",
    };

    private static readonly HashSet<string> ReparentActionProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "target",
        "parent",
    };

    /// <summary> Reads and validates the <c>actions</c> array for one public edit step. </summary>
    /// <param name="stepElement"> The raw step JSON element. </param>
    /// <param name="actions"> The parsed action contracts when validation succeeds. </param>
    /// <param name="errorMessage"> The contract violation message when validation fails. </param>
    /// <returns> <see langword="true" /> when the step contains one valid non-empty action array; otherwise <see langword="false" />. </returns>
    public static bool TryRead (
        JsonElement stepElement,
        out IReadOnlyList<IpcEditStepContract.EditAction> actions,
        out string errorMessage)
    {
        actions = Array.Empty<IpcEditStepContract.EditAction>();
        if (!IpcEditStepContractReadHelpers.TryReadRequiredArray(
            stepElement,
            "actions",
            "step.actions",
            out var actionsElement,
            out errorMessage))
        {
            return false;
        }

        var parsedActions = new List<IpcEditStepContract.EditAction>();
        var actionIndex = 0;
        foreach (var actionElement in actionsElement.EnumerateArray())
        {
            if (actionElement.ValueKind != JsonValueKind.Object)
            {
                errorMessage = $"Edit step property 'step.actions[{actionIndex}]' must be an object.";
                return false;
            }

            if (!TryReadAction(actionElement, actionIndex, out var action, out errorMessage))
            {
                return false;
            }

            parsedActions.Add(action);
            actionIndex++;
        }

        if (parsedActions.Count == 0)
        {
            errorMessage = "Edit step property 'step.actions' must contain at least one action.";
            return false;
        }

        actions = parsedActions;
        return true;
    }

    private static bool TryReadAction (
        JsonElement actionElement,
        int actionIndex,
        out IpcEditStepContract.EditAction action,
        out string errorMessage)
    {
        action = default!;
        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(
            actionElement,
            "kind",
            $"step.actions[{actionIndex}].kind",
            out var kindLiteral,
            out errorMessage)
            || !IpcCamelCaseEnumLiteralParser.TryParse(kindLiteral!, out IpcEditStepContract.ActionKind actionKind))
        {
            errorMessage = string.IsNullOrEmpty(errorMessage)
                ? $"Edit step property 'step.actions[{actionIndex}].kind' is unsupported."
                : errorMessage;
            return false;
        }

        var target = IpcEditStepContractReadHelpers.TryReadOptionalString(
            actionElement,
            "target",
            $"step.actions[{actionIndex}].target",
            out errorMessage);
        if (errorMessage.Length != 0)
        {
            return false;
        }

        var alias = IpcEditStepContractReadHelpers.TryReadOptionalString(
            actionElement,
            "as",
            $"step.actions[{actionIndex}].as",
            out errorMessage);
        if (errorMessage.Length != 0)
        {
            return false;
        }

        var type = IpcEditStepContractReadHelpers.TryReadOptionalString(
            actionElement,
            "type",
            $"step.actions[{actionIndex}].type",
            out errorMessage);
        if (errorMessage.Length != 0)
        {
            return false;
        }

        var name = IpcEditStepContractReadHelpers.TryReadOptionalString(
            actionElement,
            "name",
            $"step.actions[{actionIndex}].name",
            out errorMessage);
        if (errorMessage.Length != 0)
        {
            return false;
        }

        var path = IpcEditStepContractReadHelpers.TryReadOptionalString(
            actionElement,
            "path",
            $"step.actions[{actionIndex}].path",
            out errorMessage);
        if (errorMessage.Length != 0)
        {
            return false;
        }

        var parent = IpcEditStepContractReadHelpers.TryReadOptionalString(
            actionElement,
            "parent",
            $"step.actions[{actionIndex}].parent",
            out errorMessage);
        if (errorMessage.Length != 0)
        {
            return false;
        }

        var values = default(JsonElement);
        var hasValues = false;
        if (actionElement.TryGetProperty("values", out var valuesElement))
        {
            if (valuesElement.ValueKind != JsonValueKind.Object)
            {
                errorMessage = $"Edit step property 'step.actions[{actionIndex}].values' must be an object.";
                return false;
            }

            hasValues = true;
            values = valuesElement.Clone();
        }

        if (!ValidateActionShape(
            actionElement,
            actionIndex,
            actionKind,
            hasValues,
            values,
            target,
            type,
            name,
            path,
            parent,
            out errorMessage))
        {
            return false;
        }

        action = new IpcEditStepContract.EditAction(
            Kind: actionKind,
            Target: target,
            Alias: alias,
            Type: type,
            Name: name,
            Path: path,
            Parent: parent,
            Values: hasValues ? values : default);
        return true;
    }

    private static bool ValidateActionShape (
        JsonElement actionElement,
        int actionIndex,
        IpcEditStepContract.ActionKind actionKind,
        bool hasValues,
        JsonElement values,
        string? target,
        string? type,
        string? name,
        string? path,
        string? parent,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var allowedProperties = GetAllowedActionProperties(actionKind);
        foreach (var property in actionElement.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                errorMessage = $"Edit step property 'step.actions[{actionIndex}]' contains an unknown property: {property.Name}.";
                return false;
            }
        }

        switch (actionKind)
        {
            case IpcEditStepContract.ActionKind.Set:
                if (!hasValues)
                {
                    errorMessage = $"Edit step property 'step.actions[{actionIndex}].values' is required.";
                    return false;
                }

                if (!HasAtLeastOneProperty(values))
                {
                    errorMessage = $"Edit step property 'step.actions[{actionIndex}].values' must contain at least one assignment.";
                    return false;
                }

                return true;
            case IpcEditStepContract.ActionKind.EnsureComponent:
                if (type is null)
                {
                    errorMessage = $"Edit step property 'step.actions[{actionIndex}].type' is required.";
                    return false;
                }

                return true;
            case IpcEditStepContract.ActionKind.CreateObject:
                if (name is null)
                {
                    errorMessage = $"Edit step property 'step.actions[{actionIndex}].name' is required.";
                    return false;
                }

                return true;
            case IpcEditStepContract.ActionKind.CreateAsset:
                if (type is null || path is null)
                {
                    errorMessage = "Edit step action 'createAsset' requires both 'type' and 'path'.";
                    return false;
                }

                return true;
            case IpcEditStepContract.ActionKind.CreatePrefab:
                if (target is null || path is null)
                {
                    errorMessage = "Edit step action 'createPrefab' requires both 'target' and 'path'.";
                    return false;
                }

                return true;
            case IpcEditStepContract.ActionKind.Delete:
                return true;
            case IpcEditStepContract.ActionKind.Reparent:
                if (parent is null)
                {
                    errorMessage = $"Edit step property 'step.actions[{actionIndex}].parent' is required.";
                    return false;
                }

                return true;
            default:
                errorMessage = $"Unsupported edit action kind '{actionKind}'.";
                return false;
        }
    }

    private static HashSet<string> GetAllowedActionProperties (IpcEditStepContract.ActionKind actionKind)
    {
        return actionKind switch
        {
            IpcEditStepContract.ActionKind.Set => SetActionProperties,
            IpcEditStepContract.ActionKind.EnsureComponent => EnsureComponentActionProperties,
            IpcEditStepContract.ActionKind.CreateObject => CreateObjectActionProperties,
            IpcEditStepContract.ActionKind.CreateAsset => CreateAssetActionProperties,
            IpcEditStepContract.ActionKind.CreatePrefab => CreatePrefabActionProperties,
            IpcEditStepContract.ActionKind.Delete => DeleteActionProperties,
            IpcEditStepContract.ActionKind.Reparent => ReparentActionProperties,
            _ => SetActionProperties,
        };
    }

    private static bool HasAtLeastOneProperty (JsonElement element)
    {
        var enumerator = element.EnumerateObject();
        return enumerator.MoveNext();
    }
}
