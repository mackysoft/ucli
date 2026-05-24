using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads <c>step.select.projectAsset</c> selector objects. </summary>
internal static class IpcEditStepProjectAssetSelectorReader
{
    public static bool TryReadPath (
        JsonElement projectAssetElement,
        out string? projectAssetPath,
        out string errorMessage)
    {
        projectAssetPath = null;
        if (projectAssetElement.ValueKind != JsonValueKind.Object)
        {
            errorMessage = "Edit step property 'step.select.projectAsset' must be an object.";
            return false;
        }

        return TryReadObjectPath(projectAssetElement, out projectAssetPath, out errorMessage);
    }

    private static bool TryReadObjectPath (
        JsonElement projectAssetElement,
        out string? projectAssetPath,
        out string errorMessage)
    {
        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(
            projectAssetElement,
            "path",
            "step.select.projectAsset.path",
            out projectAssetPath,
            out errorMessage))
        {
            return false;
        }

        return TryValidateProperties(projectAssetElement, out errorMessage);
    }

    private static bool TryValidateProperties (
        JsonElement projectAssetElement,
        out string errorMessage)
    {
        foreach (var property in projectAssetElement.EnumerateObject())
        {
            if (!string.Equals(property.Name, "path", StringComparison.Ordinal))
            {
                errorMessage = $"Edit step property 'step.select.projectAsset' contains an unknown property: {property.Name}.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }
}
