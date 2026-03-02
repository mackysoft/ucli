namespace MackySoft.Ucli.Contracts.Paths;

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
}