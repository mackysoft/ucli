namespace MackySoft.Ucli.Contracts;

/// <summary> Defines shared syntax rules for Unity project-relative asset paths. </summary>
public static class UnityAssetPathContract
{
    /// <summary> Gets the canonical Unity <c>Assets</c> root path. </summary>
    public const string AssetsRootPath = "Assets";

    /// <summary> Gets the canonical Unity <c>Assets/</c> descendant prefix. </summary>
    public const string AssetsRootPrefix = "Assets/";

    private const string ProjectSettingsRootPrefix = "ProjectSettings/";

    /// <summary> Gets the Unity scene asset extension. </summary>
    public const string SceneAssetExtension = ".unity";

    /// <summary> Gets the Unity prefab asset extension. </summary>
    public const string PrefabAssetExtension = ".prefab";

    /// <summary> Gets the Unity meta-file extension. </summary>
    public const string MetaFileExtension = ".meta";

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

    /// <summary> Determines whether <paramref name="path" /> is a normalized Unity Build Profile asset path under <c>Assets/</c> and not a <c>.meta</c> file. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is slash-separated, normalized, under <c>Assets/</c>, and does not reference a Unity <c>.meta</c> file; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedBuildProfileAssetPath (string? path)
    {
        return path != null
            && IsNormalizedAssetsDescendantPath(path)
            && !path.EndsWith(MetaFileExtension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary> Normalizes and validates one Unity Build Profile asset path under <c>Assets/</c> that must not reference a <c>.meta</c> file. </summary>
    /// <param name="path"> The input path. </param>
    /// <param name="normalizedPath"> The slash-separated normalized Unity Build Profile asset path under <c>Assets/</c> when validation succeeds. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> can be normalized to an <c>Assets/</c> descendant Unity Build Profile asset path that does not reference a Unity <c>.meta</c> file; otherwise <see langword="false" />. </returns>
    public static bool TryNormalizeBuildProfileAssetPath (
        string? path,
        out string normalizedPath)
    {
        if (TryNormalizeAssetsDescendantPath(path, out normalizedPath)
            && IsNormalizedBuildProfileAssetPath(normalizedPath))
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

    /// <summary> Determines whether <paramref name="path" /> is a normalized path under <c>ProjectSettings/</c>. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is a normalized <c>ProjectSettings/</c> descendant; otherwise <see langword="false" />. </returns>
    private static bool IsNormalizedProjectSettingsDescendantPath (string? path)
    {
        return path != null
            && RelativePathContract.IsNormalized(path)
            && path.StartsWith(ProjectSettingsRootPrefix, StringComparison.Ordinal);
    }

    /// <summary> Normalizes and validates one path under <c>ProjectSettings/</c>. </summary>
    /// <param name="path"> The input path. </param>
    /// <param name="normalizedPath"> The slash-separated normalized path when validation succeeds. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> can be normalized to a <c>ProjectSettings/</c> descendant; otherwise <see langword="false" />. </returns>
    internal static bool TryNormalizeProjectSettingsDescendantPath (
        string? path,
        out string normalizedPath)
    {
        if (RelativePathContract.TryNormalize(path, out normalizedPath)
            && IsNormalizedProjectSettingsDescendantPath(normalizedPath))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

    internal static string NormalizeAssetsDescendantPathOrThrow (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return TryNormalizeAssetsDescendantPath(value, out var normalizedPath)
            ? normalizedPath
            : throw CreateInvalidPathException("Unity asset path must identify an Assets descendant.");
    }

    internal static string NormalizeAssetsRootOrDescendantPathOrThrow (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return TryNormalizeAssetsRootOrDescendantPath(value, out var normalizedPath)
            ? normalizedPath
            : throw CreateInvalidPathException("Unity asset path prefix must identify Assets or one of its descendants.");
    }

    internal static string NormalizeSceneAssetPathOrThrow (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return TryNormalizeSceneAssetPath(value, out var normalizedPath)
            ? normalizedPath
            : throw CreateInvalidPathException("Unity scene path must identify an Assets descendant with the .unity extension.");
    }

    internal static string NormalizePrefabAssetPathOrThrow (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return TryNormalizePrefabAssetPath(value, out var normalizedPath)
            ? normalizedPath
            : throw CreateInvalidPathException("Unity prefab path must identify an Assets descendant with the .prefab extension.");
    }

    internal static string NormalizeProjectSettingsDescendantPathOrThrow (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return TryNormalizeProjectSettingsDescendantPath(value, out var normalizedPath)
            ? normalizedPath
            : throw CreateInvalidPathException("ProjectSettings asset path must identify a ProjectSettings descendant.");
    }

    private static ArgumentException CreateInvalidPathException (string message)
    {
        return new ArgumentException(message, "value");
    }

}
