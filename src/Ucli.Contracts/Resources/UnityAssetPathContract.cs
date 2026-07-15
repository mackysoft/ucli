using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Ucli.Contracts;

/// <summary> Defines shared syntax rules for Unity project-relative asset paths. </summary>
public static class UnityAssetPathContract
{
    /// <summary> Gets the canonical Unity <c>Assets</c> root path. </summary>
    public const string AssetsRootPath = "Assets";

    /// <summary> Gets the canonical Unity <c>Assets/</c> descendant prefix. </summary>
    public const string AssetsRootPrefix = "Assets/";

    /// <summary> Gets the canonical Unity <c>Packages/</c> descendant prefix. </summary>
    public const string PackagesRootPrefix = "Packages/";

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
    public static bool IsNormalizedAssetsRootOrDescendantPath ([NotNullWhen(true)] string? path)
    {
        return path != null
            && RelativePathContract.IsNormalized(path)
            && IsAssetsRootOrDescendantPath(path);
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
            && IsAssetsRootOrDescendantPath(normalizedPath))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

    /// <summary> Determines whether <paramref name="path" /> is a normalized path under <c>Assets/</c>. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is an <c>Assets/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedAssetsDescendantPath ([NotNullWhen(true)] string? path)
    {
        return path != null
            && RelativePathContract.IsNormalized(path)
            && IsAssetsDescendantPath(path);
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
            && IsAssetsDescendantPath(normalizedPath))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

    /// <summary> Determines whether one normalized asset path is equal to a normalized path prefix or is located below it. </summary>
    /// <param name="normalizedPathPrefix"> The normalized <c>Assets</c> root or descendant path used as the segment boundary. </param>
    /// <param name="normalizedAssetPath"> The normalized <c>Assets/</c> descendant path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="normalizedAssetPath" /> is the same path or a descendant separated by <c>/</c>; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when either argument is <see langword="null" />. </exception>
    /// <remarks> <paramref name="normalizedPathPrefix" /> must satisfy <see cref="IsNormalizedAssetsRootOrDescendantPath" />, and <paramref name="normalizedAssetPath" /> must satisfy <see cref="IsNormalizedAssetsDescendantPath" />. This comparison does not normalize or revalidate either value. </remarks>
    internal static bool IsSameOrDescendantAssetPath (
        string normalizedPathPrefix,
        string normalizedAssetPath)
    {
        if (normalizedPathPrefix == null)
        {
            throw new ArgumentNullException(nameof(normalizedPathPrefix));
        }

        if (normalizedAssetPath == null)
        {
            throw new ArgumentNullException(nameof(normalizedAssetPath));
        }

        return normalizedAssetPath.StartsWith(normalizedPathPrefix, StringComparison.Ordinal)
            && (normalizedAssetPath.Length == normalizedPathPrefix.Length
                || (normalizedAssetPath.Length > normalizedPathPrefix.Length
                    && normalizedAssetPath[normalizedPathPrefix.Length] == '/'));
    }

    /// <summary> Determines whether <paramref name="path" /> is a normalized Unity scene asset path under <c>Assets/</c>. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is a normalized scene asset path; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedSceneAssetPath ([NotNullWhen(true)] string? path)
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
            && normalizedPath.EndsWith(SceneAssetExtension, StringComparison.Ordinal))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

    /// <summary> Normalizes a Unity scene path under <c>Assets/</c> or <c>Packages/</c>. </summary>
    internal static bool TryNormalizeUnityScenePath (
        string? path,
        out string normalizedPath)
    {
        if (RelativePathContract.TryNormalize(path, out normalizedPath)
            && IsUnityScenePath(normalizedPath))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
    }

    /// <summary> Determines whether <paramref name="path" /> is a normalized Unity prefab asset path under <c>Assets/</c>. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is a normalized prefab asset path; otherwise <see langword="false" />. </returns>
    public static bool IsNormalizedPrefabAssetPath ([NotNullWhen(true)] string? path)
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
            && normalizedPath.EndsWith(PrefabAssetExtension, StringComparison.Ordinal))
        {
            return true;
        }

        normalizedPath = string.Empty;
        return false;
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
            && normalizedPath.StartsWith(ProjectSettingsRootPrefix, StringComparison.Ordinal))
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

    internal static string NormalizeUnityScenePathOrThrow (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return TryNormalizeUnityScenePath(value, out var normalizedPath)
            ? normalizedPath
            : throw CreateInvalidPathException("Unity scene path must identify an Assets or Packages descendant with the .unity extension.");
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

    private static bool IsAssetsRootOrDescendantPath (string path)
    {
        return string.Equals(path, AssetsRootPath, StringComparison.Ordinal)
            || IsAssetsDescendantPath(path);
    }

    private static bool IsAssetsDescendantPath (string path)
    {
        return path.StartsWith(AssetsRootPrefix, StringComparison.Ordinal);
    }

    private static bool IsUnityScenePath (string path)
    {
        return (IsAssetsDescendantPath(path)
                || path.StartsWith(PackagesRootPrefix, StringComparison.Ordinal))
            && path.EndsWith(SceneAssetExtension, StringComparison.Ordinal);
    }

}
