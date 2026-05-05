using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Reads the <c>select</c> object for one public edit step. </summary>
internal static class IpcEditStepSelectionReader
{
    private static readonly ISet<string> AllowedFromSelectionProperties = new HashSet<string>
    {
        "cardinality",
        "from",
    };

    private static readonly ISet<string> AllowedFromProperties = new HashSet<string>
    {
        "op",
        "args",
    };

    public static bool TryRead (
        JsonElement stepElement,
        IpcEditStepContract.ContextKind contextKind,
        out IpcEditStepContract.EditSelection selection,
        out string errorMessage)
    {
        selection = default!;
        if (!IpcEditStepContractReadHelpers.TryReadRequiredObject(
            stepElement,
            "select",
            "step.select",
            out var selectElement,
            out errorMessage))
        {
            return false;
        }

        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(
            selectElement,
            "cardinality",
            "step.select.cardinality",
            out var cardinalityLiteral,
            out errorMessage)
            || !IpcCamelCaseEnumLiteralParser.TryParse(cardinalityLiteral!, out IpcEditStepContract.CardinalityKind cardinality))
        {
            errorMessage = string.IsNullOrEmpty(errorMessage)
                ? "Edit step property 'step.select.cardinality' must be one of 'one', 'first', 'all', or 'atMostOne'."
                : errorMessage;
            return false;
        }

        if (selectElement.TryGetProperty("from", out var fromElement))
        {
            if (contextKind != IpcEditStepContract.ContextKind.Scene)
            {
                errorMessage = "Edit step property 'step.select.from' is supported only for scene context.";
                return false;
            }

            if (HasDirectSelectorProperty(selectElement))
            {
                errorMessage = "Edit step property 'step.select' cannot mix 'from' with direct selector fields.";
                return false;
            }

            var unknownSelectProperty = JsonPropertyGuard.FindUnknownProperty(selectElement, AllowedFromSelectionProperties);
            if (unknownSelectProperty is not null)
            {
                errorMessage = $"Edit step property 'step.select' contains an unknown property: {unknownSelectProperty}.";
                return false;
            }

            return TryReadFromSelection(fromElement, cardinality, out selection, out errorMessage);
        }

        return TryReadDirectSelection(selectElement, contextKind, cardinality, out selection, out errorMessage);
    }

    private static bool TryReadFromSelection (
        JsonElement fromElement,
        IpcEditStepContract.CardinalityKind cardinality,
        out IpcEditStepContract.EditSelection selection,
        out string errorMessage)
    {
        selection = default!;
        if (fromElement.ValueKind != JsonValueKind.Object)
        {
            errorMessage = "Edit step property 'step.select.from' must be an object.";
            return false;
        }

        var unknownFromProperty = JsonPropertyGuard.FindUnknownProperty(fromElement, AllowedFromProperties);
        if (unknownFromProperty is not null)
        {
            errorMessage = $"Edit step property 'step.select.from' contains an unknown property: {unknownFromProperty}.";
            return false;
        }

        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(
            fromElement,
            "op",
            "step.select.from.op",
            out var sourceOperation,
            out errorMessage))
        {
            return false;
        }

        if (!string.Equals(sourceOperation, UcliPrimitiveOperationNames.SceneQuery, StringComparison.Ordinal))
        {
            errorMessage = $"Edit step property 'step.select.from.op' must be '{UcliPrimitiveOperationNames.SceneQuery}'.";
            return false;
        }

        if (!IpcEditStepContractReadHelpers.TryReadRequiredObject(
            fromElement,
            "args",
            "step.select.from.args",
            out var sourceArgs,
            out errorMessage))
        {
            return false;
        }

        if (!IpcSceneQueryArgsContractReader.TryReadForEditSelection(sourceArgs, out _, out errorMessage))
        {
            return false;
        }

        selection = new IpcEditStepContract.EditSelection(
            Kind: IpcEditStepContract.SelectionKind.From,
            Cardinality: cardinality,
            GameObjectPath: null,
            ComponentType: null,
            Self: false,
            ProjectAssetPath: null,
            SourceOperation: sourceOperation,
            SourceArgs: sourceArgs.Clone());
        return true;
    }

    private static bool TryReadDirectSelection (
        JsonElement selectElement,
        IpcEditStepContract.ContextKind contextKind,
        IpcEditStepContract.CardinalityKind cardinality,
        out IpcEditStepContract.EditSelection selection,
        out string errorMessage)
    {
        selection = default!;
        errorMessage = string.Empty;
        string? gameObjectPath = null;
        string? componentType = null;
        var self = false;
        string? projectAssetPath = null;
        var hasGameObject = false;
        var hasComponent = false;
        var hasSelf = false;
        var hasProjectAsset = false;

        foreach (var property in selectElement.EnumerateObject())
        {
            if (string.Equals(property.Name, "cardinality", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(property.Name, "gameObject", StringComparison.Ordinal))
            {
                if (!IpcEditStepContractReadHelpers.TryReadUniqueString(
                    property,
                    "step.select.gameObject",
                    ref hasGameObject,
                    out gameObjectPath,
                    out errorMessage))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(property.Name, "component", StringComparison.Ordinal))
            {
                if (!IpcEditStepContractReadHelpers.TryReadUniqueString(
                    property,
                    "step.select.component",
                    ref hasComponent,
                    out componentType,
                    out errorMessage))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(property.Name, "self", StringComparison.Ordinal))
            {
                if (hasSelf)
                {
                    errorMessage = "Edit step property 'step.select.self' is duplicated.";
                    return false;
                }

                if (property.Value.ValueKind != JsonValueKind.True)
                {
                    errorMessage = "Edit step property 'step.select.self' must be true when specified.";
                    return false;
                }

                hasSelf = true;
                self = true;
                continue;
            }

            if (string.Equals(property.Name, "projectAsset", StringComparison.Ordinal))
            {
                if (hasProjectAsset)
                {
                    errorMessage = "Edit step property 'step.select.projectAsset' is duplicated.";
                    return false;
                }

                if (!TryReadProjectAssetPath(property.Value, out projectAssetPath, out errorMessage))
                {
                    return false;
                }

                hasProjectAsset = true;
                continue;
            }

            errorMessage = $"Edit step property 'step.select' contains an unknown property: {property.Name}.";
            return false;
        }

        switch (contextKind)
        {
            case IpcEditStepContract.ContextKind.Scene:
            case IpcEditStepContract.ContextKind.Prefab:
                if (!hasGameObject)
                {
                    errorMessage = "Edit step property 'step.select.gameObject' is required for scene/prefab context.";
                    return false;
                }

                if (hasSelf || hasProjectAsset)
                {
                    errorMessage = "Edit step direct selector for scene/prefab context can only use 'gameObject' and optional 'component'.";
                    return false;
                }

                break;

            case IpcEditStepContract.ContextKind.Asset:
                if (!hasSelf || hasGameObject || hasComponent || hasProjectAsset)
                {
                    errorMessage = "Edit step direct selector for asset context must use only 'self: true'.";
                    return false;
                }

                break;

            case IpcEditStepContract.ContextKind.Project:
                if (!hasProjectAsset || hasGameObject || hasComponent || hasSelf)
                {
                    errorMessage = "Edit step direct selector for project context must use only 'projectAsset.path'.";
                    return false;
                }

                break;
        }

        selection = new IpcEditStepContract.EditSelection(
            Kind: IpcEditStepContract.SelectionKind.Direct,
            Cardinality: cardinality,
            GameObjectPath: gameObjectPath,
            ComponentType: componentType,
            Self: self,
            ProjectAssetPath: projectAssetPath,
            SourceOperation: null,
            SourceArgs: default);
        return true;
    }

    private static bool TryReadProjectAssetPath (
        JsonElement projectAssetElement,
        out string? projectAssetPath,
        out string errorMessage)
    {
        projectAssetPath = null;
        errorMessage = string.Empty;
        if (projectAssetElement.ValueKind != JsonValueKind.Object)
        {
            errorMessage = "Edit step property 'step.select.projectAsset' must be an object.";
            return false;
        }

        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(
            projectAssetElement,
            "path",
            "step.select.projectAsset.path",
            out projectAssetPath,
            out errorMessage))
        {
            return false;
        }

        foreach (var property in projectAssetElement.EnumerateObject())
        {
            if (!string.Equals(property.Name, "path", StringComparison.Ordinal))
            {
                errorMessage = $"Edit step property 'step.select.projectAsset' contains an unknown property: {property.Name}.";
                return false;
            }
        }

        return true;
    }

    private static bool HasDirectSelectorProperty (JsonElement selectElement)
    {
        return selectElement.TryGetProperty("gameObject", out _)
               || selectElement.TryGetProperty("component", out _)
               || selectElement.TryGetProperty("self", out _)
               || selectElement.TryGetProperty("projectAsset", out _);
    }
}
