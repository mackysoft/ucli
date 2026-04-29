using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets.Access;

/// <summary> Provides shared helpers used by asset lookup access services. </summary>
internal static class AssetLookupAccessUtilities
{
    private const string AssetsRootPath = "Assets";
    private const string AssetsRootPrefix = "Assets/";

    /// <summary> Combines two fallback reason fragments into one user-facing sentence. </summary>
    public static string? CombineFallbackReasons (
        string? first,
        string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return string.IsNullOrWhiteSpace(second) ? null : second;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first;
        }

        return $"{first} {second}";
    }

    /// <summary> Converts one freshness value to its wire-format literal. </summary>
    public static string DescribeFreshness (IndexFreshness freshness)
    {
        return freshness switch
        {
            IndexFreshness.Fresh => "fresh",
            IndexFreshness.Probable => "probable",
            IndexFreshness.Stale => "stale",
            _ => throw new ArgumentOutOfRangeException(nameof(freshness), freshness, "Unsupported index freshness."),
        };
    }

    /// <summary> Normalizes and validates one assets.find-style query. </summary>
    public static bool TryNormalizeSearchQuery (
        AssetSearchLookupQuery query,
        out AssetSearchLookupQuery normalizedQuery,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(query);

        normalizedQuery = query;
        errorMessage = string.Empty;
        var typeId = query.TypeId;
        if (!TryNormalizeOptionalStrictString(typeId, "type", out typeId, out errorMessage))
        {
            return false;
        }

        var pathPrefix = query.PathPrefix;
        if (!TryNormalizeOptionalStrictString(pathPrefix, "pathPrefix", out pathPrefix, out errorMessage))
        {
            return false;
        }

        if (pathPrefix != null)
        {
            pathPrefix = PathStringNormalizer.ToSlashSeparated(pathPrefix);
            if (!IsAssetsRootOrDescendant(pathPrefix))
            {
                errorMessage = $"Path prefix must be 'Assets' or one of its descendants. Actual: {pathPrefix}.";
                return false;
            }
        }

        var nameContains = query.NameContains;
        if (!TryNormalizeOptionalStrictString(nameContains, "nameContains", out nameContains, out errorMessage))
        {
            return false;
        }

        if (typeId == null
            && pathPrefix == null
            && nameContains == null)
        {
            errorMessage = "At least one of 'type', 'pathPrefix', or 'nameContains' must be specified.";
            return false;
        }

        normalizedQuery = new AssetSearchLookupQuery(typeId, pathPrefix, nameContains);
        return true;
    }

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

        normalizedAssetPath = PathStringNormalizer.ToSlashSeparated(normalizedAssetPath);
        if (!IsAssetsRootOrDescendant(normalizedAssetPath))
        {
            errorMessage = $"Asset path must be 'Assets' or one of its descendants. Actual: {normalizedAssetPath}.";
            return false;
        }

        return true;
    }

    private static bool TryNormalizeOptionalStrictString (
        string? value,
        string propertyName,
        out string? normalizedValue,
        out string errorMessage)
    {
        normalizedValue = value;
        errorMessage = string.Empty;
        if (value == null)
        {
            return true;
        }

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

    private static bool IsAssetsRootOrDescendant (string assetPath)
    {
        return string.Equals(assetPath, AssetsRootPath, StringComparison.Ordinal)
            || assetPath.StartsWith(AssetsRootPrefix, StringComparison.Ordinal);
    }
}
