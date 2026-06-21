namespace MackySoft.Ucli.Contracts;

/// <summary> Defines shared syntax rules for Unity project-relative asset paths. </summary>
public static class UnityAssetPathContract
{
    /// <summary> Gets the canonical Unity <c>Assets</c> root path. </summary>
    public const string AssetsRootPath = "Assets";

    /// <summary> Gets the canonical Unity <c>Assets/</c> descendant prefix. </summary>
    public const string AssetsRootPrefix = "Assets/";

    /// <summary> Gets the Unity scene asset extension. </summary>
    public const string SceneAssetExtension = ".unity";

    /// <summary> Gets the Unity prefab asset extension. </summary>
    public const string PrefabAssetExtension = ".prefab";

    /// <summary> Determines whether <paramref name="path" /> is the normalized <c>Assets</c> root or a normalized path under it. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is <c>Assets</c> or an <c>Assets/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedAssetsRootOrDescendantPath (string? path)
    {
        if (path == null || !RelativePathContract.IsNormalized(path))
        {
            return false;
        }

        return string.Equals(path, AssetsRootPath, StringComparison.Ordinal)
            || path.StartsWith(AssetsRootPrefix, StringComparison.Ordinal);
    }

    /// <summary> Normalizes and validates one path that must identify the <c>Assets</c> root or a path under it. </summary>
    /// <param name="path"> The input path. </param>
    /// <param name="normalizedPath"> The slash-separated normalized path when validation succeeds. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> can be normalized to <c>Assets</c> or an <c>Assets/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool TryNormalizeAssetsRootOrDescendantPath (
        string? path,
        out string normalizedPath)
    {
        if (RelativePathContract.TryNormalize(path, out normalizedPath)
            && IsNormalizedAssetsRootOrDescendantPath(normalizedPath))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

    /// <summary> Determines whether <paramref name="path" /> is a normalized path under <c>Assets/</c>. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is an <c>Assets/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedAssetsDescendantPath (string? path)
    {
        return path != null
            && RelativePathContract.IsNormalized(path)
            && path.StartsWith(AssetsRootPrefix, StringComparison.Ordinal);
    }

    /// <summary> Normalizes and validates one path that must identify an asset under <c>Assets/</c>. </summary>
    /// <param name="path"> The input path. </param>
    /// <param name="normalizedPath"> The slash-separated normalized path when validation succeeds. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> can be normalized to an <c>Assets/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool TryNormalizeAssetsDescendantPath (
        string? path,
        out string normalizedPath)
    {
        if (RelativePathContract.TryNormalize(path, out normalizedPath)
            && IsNormalizedAssetsDescendantPath(normalizedPath))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

    /// <summary> Determines whether <paramref name="path" /> is a normalized Unity scene asset path under <c>Assets/</c>. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is a normalized scene asset path; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedSceneAssetPath (string? path)
    {
        return path != null
            && IsNormalizedAssetsDescendantPath(path)
            && path.EndsWith(SceneAssetExtension, StringComparison.Ordinal);
    }

    /// <summary> Normalizes and validates one Unity scene asset path under <c>Assets/</c>. </summary>
    /// <param name="path"> The input path. </param>
    /// <param name="normalizedPath"> The slash-separated normalized path when validation succeeds. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> can be normalized to a Unity scene asset path; otherwise <see langword="false" />. </returns>
    public static bool TryNormalizeSceneAssetPath (
        string? path,
        out string normalizedPath)
    {
        if (TryNormalizeAssetsDescendantPath(path, out normalizedPath)
            && IsNormalizedSceneAssetPath(normalizedPath))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

    /// <summary> Determines whether <paramref name="path" /> is a normalized Unity prefab asset path under <c>Assets/</c>. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is a normalized prefab asset path; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedPrefabAssetPath (string? path)
    {
        return path != null
            && IsNormalizedAssetsDescendantPath(path)
            && path.EndsWith(PrefabAssetExtension, StringComparison.Ordinal);
    }

    /// <summary> Normalizes and validates one Unity prefab asset path under <c>Assets/</c>. </summary>
    /// <param name="path"> The input path. </param>
    /// <param name="normalizedPath"> The slash-separated normalized path when validation succeeds. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> can be normalized to a Unity prefab asset path; otherwise <see langword="false" />. </returns>
    public static bool TryNormalizePrefabAssetPath (
        string? path,
        out string normalizedPath)
    {
        if (TryNormalizeAssetsDescendantPath(path, out normalizedPath)
            && IsNormalizedPrefabAssetPath(normalizedPath))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

}
