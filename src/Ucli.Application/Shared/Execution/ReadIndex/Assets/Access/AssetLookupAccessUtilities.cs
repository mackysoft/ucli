using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Provides shared helpers used by asset lookup access services. </summary>
internal static class AssetLookupAccessUtilities
{
    /// <summary> Normalizes one asset GUID input. </summary>
    public static bool TryNormalizeAssetGuid (
        string assetGuid,
        out string normalizedAssetGuid,
        out string errorMessage)
    {
        return TryNormalizeRequiredStrictString(assetGuid, "assetGuid", out normalizedAssetGuid, out errorMessage);
    }

    /// <summary> Normalizes one asset path input. </summary>
    public static bool TryNormalizeAssetPath (
        string assetPath,
        out string normalizedAssetPath,
        out string errorMessage)
    {
        if (!TryNormalizeRequiredStrictString(assetPath, "assetPath", out normalizedAssetPath, out errorMessage))
        {
            return false;
        }

        var rawAssetPath = normalizedAssetPath;
        if (!UnityAssetPathContract.TryNormalizeAssetsRootOrDescendantPath(rawAssetPath, out normalizedAssetPath))
        {
            errorMessage = $"Asset path must be 'Assets' or one of its descendants. Actual: {rawAssetPath}.";
            return false;
        }

        return true;
    }

    private static bool TryNormalizeRequiredStrictString (
        string value,
        string propertyName,
        out string normalizedValue,
        out string errorMessage)
    {
        normalizedValue = value;
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = $"Property '{propertyName}' must not be empty or whitespace.";
            return false;
        }

        if (StringValueValidator.HasOuterWhitespace(value))
        {
            errorMessage = $"Property '{propertyName}' must not contain leading or trailing whitespace.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

}
