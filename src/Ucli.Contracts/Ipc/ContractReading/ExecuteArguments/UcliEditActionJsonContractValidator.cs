namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary>Validates the runtime DTO for one tagged edit action.</summary>
internal static class UcliEditActionJsonContractValidator
{
    public static bool TryValidate (
        UcliEditActionJsonContract? action,
        string path,
        out string errorMessage)
    {
        if (action == null)
        {
            return MissingAction(path, out errorMessage);
        }

        return action switch
        {
            UcliSetEditActionJsonContract set => TryValidateSet(set, path, out errorMessage),
            UcliEnsureComponentEditActionJsonContract ensure =>
                TryValidateEnsureComponent(ensure, path, out errorMessage),
            UcliPrefabOverridesEditActionJsonContract prefabOverrides =>
                TryValidatePrefabOverrides(prefabOverrides, path, out errorMessage),
            UcliDeleteEditActionJsonContract delete =>
                TryValidateTarget(delete.Target, path, out errorMessage),
            UcliReparentEditActionJsonContract reparent =>
                TryValidateReparent(reparent, path, out errorMessage),
            _ => TryValidateCreation(action, path, out errorMessage),
        };
    }

    private static bool TryValidateSet (
        UcliSetEditActionJsonContract action,
        string path,
        out string errorMessage)
    {
        if (action.Values == null || action.Values.Count == 0)
        {
            errorMessage = $"Request property '{path}/values' must contain at least one assignment.";
            return false;
        }

        return UcliRequestJsonTextContractValidator.TryValidateOptional(
            action.Target,
            $"{path}/target",
            out errorMessage);
    }

    private static bool TryValidateEnsureComponent (
        UcliEnsureComponentEditActionJsonContract action,
        string path,
        out string errorMessage)
    {
        return UcliRequestJsonTextContractValidator.TryValidateOptional(
                   action.Target,
                   $"{path}/target",
                   out errorMessage)
               && UcliRequestJsonTextContractValidator.TryValidate(
                   action.Type,
                   $"{path}/type",
                   out errorMessage)
               && UcliRequestJsonTextContractValidator.TryValidateOptional(
                   action.Alias,
                   $"{path}/as",
                   out errorMessage);
    }

    private static bool TryValidateCreation (
        UcliEditActionJsonContract action,
        string path,
        out string errorMessage)
    {
        return action switch
        {
            UcliCreateObjectEditActionJsonContract createObject =>
                TryValidateCreateObject(createObject, path, out errorMessage),
            UcliCreateAssetEditActionJsonContract createAsset =>
                TryValidateCreateAsset(createAsset, path, out errorMessage),
            UcliCreatePrefabEditActionJsonContract createPrefab =>
                TryValidateCreatePrefab(createPrefab, path, out errorMessage),
            _ => Unsupported(path, out errorMessage),
        };
    }

    private static bool TryValidateCreateObject (
        UcliCreateObjectEditActionJsonContract action,
        string path,
        out string errorMessage)
    {
        return UcliRequestJsonTextContractValidator.TryValidate(
                   action.Name,
                   $"{path}/name",
                   out errorMessage)
               && UcliRequestJsonTextContractValidator.TryValidateOptional(
                   action.Alias,
                   $"{path}/as",
                   out errorMessage);
    }

    private static bool TryValidateCreateAsset (
        UcliCreateAssetEditActionJsonContract action,
        string path,
        out string errorMessage)
    {
        return UcliRequestJsonTextContractValidator.TryValidate(
                   action.Type,
                   $"{path}/type",
                   out errorMessage)
               && UcliRequestJsonTextContractValidator.TryValidate(
                   action.Path,
                   $"{path}/path",
                   out errorMessage);
    }

    private static bool TryValidateCreatePrefab (
        UcliCreatePrefabEditActionJsonContract action,
        string path,
        out string errorMessage)
    {
        return UcliRequestJsonTextContractValidator.TryValidateOptional(
                   action.Target,
                   $"{path}/target",
                   out errorMessage)
               && UcliRequestJsonTextContractValidator.TryValidate(
                   action.Path,
                   $"{path}/path",
                   out errorMessage);
    }

    private static bool TryValidateTarget (
        string? target,
        string path,
        out string errorMessage)
    {
        return UcliRequestJsonTextContractValidator.TryValidateOptional(
            target,
            $"{path}/target",
            out errorMessage);
    }

    private static bool TryValidateReparent (
        UcliReparentEditActionJsonContract action,
        string path,
        out string errorMessage)
    {
        return TryValidateTarget(action.Target, path, out errorMessage)
               && UcliRequestJsonTextContractValidator.TryValidate(
                   action.Parent,
                   $"{path}/parent",
                   out errorMessage);
    }

    private static bool TryValidatePrefabOverrides (
        UcliPrefabOverridesEditActionJsonContract action,
        string path,
        out string errorMessage)
    {
        if (!UcliRequestJsonTextContractValidator.TryValidateOptional(
                action.Target,
                $"{path}/target",
                out errorMessage)
            || !UcliRequestJsonTextContractValidator.TryValidate(
                action.TargetAssetPath,
                $"{path}/targetAssetPath",
                out errorMessage))
        {
            return false;
        }

        return TryValidatePropertyPaths(action.PropertyPaths, path, out errorMessage);
    }

    private static bool TryValidatePropertyPaths (
        IReadOnlyList<string>? propertyPaths,
        string path,
        out string errorMessage)
    {
        if (propertyPaths == null)
        {
            errorMessage = string.Empty;
            return true;
        }

        if (propertyPaths.Count == 0)
        {
            errorMessage = $"Request property '{path}/propertyPaths' must contain at least one path when specified.";
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < propertyPaths.Count; i++)
        {
            var propertyPath = propertyPaths[i];
            if (!UcliRequestJsonTextContractValidator.TryValidate(
                    propertyPath,
                    $"{path}/propertyPaths/{i}",
                    out errorMessage))
            {
                return false;
            }

            if (!seen.Add(propertyPath))
            {
                errorMessage = $"Request property '{path}/propertyPaths' contains duplicate path: {propertyPath}.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool Unsupported (string path, out string errorMessage)
    {
        errorMessage = $"Request property '{path}/kind' is unsupported.";
        return false;
    }

    private static bool MissingAction (string path, out string errorMessage)
    {
        errorMessage = $"Request property '{path}' must be an object.";
        return false;
    }
}
