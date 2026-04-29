using System;
using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Reads the <c>on</c> context object for one public edit step. </summary>
internal static class IpcEditStepContextReader
{
    public static bool TryRead (
        JsonElement stepElement,
        out IpcEditStepContract.EditContext context,
        out string errorMessage)
    {
        context = default!;
        if (!IpcEditStepContractReadHelpers.TryReadRequiredObject(
            stepElement,
            "on",
            "step.on",
            out var onElement,
            out errorMessage))
        {
            return false;
        }

        var hasScene = false;
        var hasPrefab = false;
        var hasAsset = false;
        var hasProject = false;
        string? path = null;
        foreach (var property in onElement.EnumerateObject())
        {
            if (string.Equals(property.Name, "scene", StringComparison.Ordinal))
            {
                if (!IpcEditStepContractReadHelpers.TryReadUniqueString(
                    property,
                    "step.on.scene",
                    ref hasScene,
                    out path,
                    out errorMessage))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(property.Name, "prefab", StringComparison.Ordinal))
            {
                if (!IpcEditStepContractReadHelpers.TryReadUniqueString(
                    property,
                    "step.on.prefab",
                    ref hasPrefab,
                    out path,
                    out errorMessage))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(property.Name, "asset", StringComparison.Ordinal))
            {
                if (!IpcEditStepContractReadHelpers.TryReadUniqueString(
                    property,
                    "step.on.asset",
                    ref hasAsset,
                    out path,
                    out errorMessage))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(property.Name, "project", StringComparison.Ordinal))
            {
                if (hasProject)
                {
                    errorMessage = "Edit step property 'step.on.project' is duplicated.";
                    return false;
                }

                if (property.Value.ValueKind != JsonValueKind.True)
                {
                    errorMessage = "Edit step property 'step.on.project' must be true.";
                    return false;
                }

                hasProject = true;
                continue;
            }

            errorMessage = $"Edit step property 'step.on' contains an unknown property: {property.Name}.";
            return false;
        }

        var contextCount = IpcEditStepContractReadHelpers.CountTrue(hasScene, hasPrefab, hasAsset, hasProject);
        if (contextCount != 1)
        {
            errorMessage = "Edit step property 'step.on' must specify exactly one of 'scene', 'prefab', 'asset', or 'project'.";
            return false;
        }

        context = hasScene
            ? new IpcEditStepContract.EditContext(IpcEditStepContract.ContextKind.Scene, path)
            : hasPrefab
                ? new IpcEditStepContract.EditContext(IpcEditStepContract.ContextKind.Prefab, path)
                : hasAsset
                    ? new IpcEditStepContract.EditContext(IpcEditStepContract.ContextKind.Asset, path)
                    : new IpcEditStepContract.EditContext(IpcEditStepContract.ContextKind.Project, null);
        errorMessage = string.Empty;
        return true;
    }
}
