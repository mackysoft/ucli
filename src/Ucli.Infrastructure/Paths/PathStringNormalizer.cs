using System.Runtime.InteropServices;

namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Provides reusable normalization helpers for path text values. </summary>
internal static class PathStringNormalizer
{
    /// <summary> Gets the string comparison used for path identity on the current platform. </summary>
    public static StringComparison CurrentPlatformPathComparison =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary> Gets the string comparer used for path identity on the current platform. </summary>
    public static StringComparer CurrentPlatformPathComparer =>
        CurrentPlatformPathComparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary> Replaces backslashes with forward slashes. </summary>
    /// <param name="pathValue"> The path text value. </param>
    /// <returns> The slash-separated path text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pathValue" /> is <see langword="null" />. </exception>
    public static string ToSlashSeparated (string pathValue)
    {
        if (pathValue == null)
        {
            throw new ArgumentNullException(nameof(pathValue));
        }

        return pathValue.Replace('\\', '/');
    }

    /// <summary> Replaces alternate separators with the current platform separator. </summary>
    /// <param name="pathValue"> The path text value. </param>
    /// <returns> The platform-separated path text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pathValue" /> is <see langword="null" />. </exception>
    public static string ToPlatformSeparated (string pathValue)
    {
        if (pathValue == null)
        {
            throw new ArgumentNullException(nameof(pathValue));
        }

        return pathValue
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary> Replaces only alternate separators with the current platform separator. </summary>
    /// <param name="pathValue"> The path text value. </param>
    /// <returns> The platform-separated path text where only alternate separators are normalized. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pathValue" /> is <see langword="null" />. </exception>
    public static string ReplaceAltSeparatorWithPlatformSeparator (string pathValue)
    {
        if (pathValue == null)
        {
            throw new ArgumentNullException(nameof(pathValue));
        }

        return pathValue.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    /// <summary> Trims trailing directory separators from a path text value. </summary>
    /// <param name="pathValue"> The path text value. </param>
    /// <returns> The trimmed path text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pathValue" /> is <see langword="null" />. </exception>
    public static string TrimTrailingDirectorySeparators (string pathValue)
    {
        if (pathValue == null)
        {
            throw new ArgumentNullException(nameof(pathValue));
        }

        return pathValue.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary> Determines whether <paramref name="pathValue" /> is the current-platform filesystem root path. </summary>
    /// <param name="pathValue"> The path text value. </param>
    /// <returns> <see langword="true" /> when the path is a filesystem root; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pathValue" /> is <see langword="null" />. </exception>
    public static bool IsPathRoot (string pathValue)
    {
        if (pathValue == null)
        {
            throw new ArgumentNullException(nameof(pathValue));
        }

        var pathRoot = Path.GetPathRoot(pathValue);
        return !string.IsNullOrEmpty(pathRoot)
            && string.Equals(pathValue, pathRoot, CurrentPlatformPathComparison);
    }

    /// <summary> Normalizes path casing for platforms with case-insensitive path semantics. </summary>
    /// <param name="pathValue"> The path text value. </param>
    /// <returns> The path text where case differences are normalized on case-insensitive platforms. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pathValue" /> is <see langword="null" />. </exception>
    public static string NormalizeCaseForCurrentPlatform (string pathValue)
    {
        if (pathValue == null)
        {
            throw new ArgumentNullException(nameof(pathValue));
        }

        return CurrentPlatformPathComparison == StringComparison.OrdinalIgnoreCase
            ? pathValue.ToUpperInvariant()
            : pathValue;
    }

    /// <summary> Normalizes one absolute path value for deterministic hash input. </summary>
    /// <param name="pathValue"> The path text value. </param>
    /// <returns> The absolute path text with slash and case normalization applied. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pathValue" /> is <see langword="null" />. </exception>
    public static string NormalizeAbsolutePathForHash (string pathValue)
    {
        if (pathValue == null)
        {
            throw new ArgumentNullException(nameof(pathValue));
        }

        var pathResult = PathNormalizer.TryNormalizeFullPath(pathValue);
        if (!pathResult.IsSuccess)
        {
            throw new ArgumentException(pathResult.DiagnosticMessage, nameof(pathValue));
        }

        return NormalizeCaseForCurrentPlatform(ToSlashSeparated(pathResult.FullPath!));
    }

    /// <summary> Normalizes one absolute path value for stable filesystem identity comparisons. </summary>
    /// <param name="pathValue"> The path text value. </param>
    /// <returns> The absolute path text with platform separator, trailing separator, and case normalization applied. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pathValue" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="pathValue" /> cannot be normalized as a full path. </exception>
    public static string NormalizeAbsolutePathForStableIdentity (string pathValue)
    {
        if (pathValue == null)
        {
            throw new ArgumentNullException(nameof(pathValue));
        }

        var pathResult = PathNormalizer.TryNormalizeFullPath(pathValue);
        if (!pathResult.IsSuccess)
        {
            throw new ArgumentException(pathResult.DiagnosticMessage, nameof(pathValue));
        }

        var fullPath = ReplaceAltSeparatorWithPlatformSeparator(pathResult.FullPath!);
        if (IsPathRoot(fullPath))
        {
            return NormalizeCaseForCurrentPlatform(fullPath);
        }

        return NormalizeCaseForCurrentPlatform(TrimTrailingDirectorySeparators(fullPath));
    }
}
