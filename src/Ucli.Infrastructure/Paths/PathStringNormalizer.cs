using System.Runtime.InteropServices;

namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Provides reusable normalization helpers for path text values. </summary>
internal static class PathStringNormalizer
{
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

        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
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

        return NormalizeCaseForCurrentPlatform(ToSlashSeparated(Path.GetFullPath(pathValue)));
    }
}
