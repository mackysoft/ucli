namespace MackySoft.Ucli.Contracts;

/// <summary> Provides shared text operations for portable slash-separated contract paths. </summary>
internal static class PortablePathText
{
    /// <summary> Replaces alternate path separators with forward slashes. </summary>
    /// <param name="pathValue"> The path text value. </param>
    /// <returns> The path text with forward slashes. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pathValue" /> is <see langword="null" />. </exception>
    public static string ToSlashSeparated (string pathValue)
    {
        if (pathValue == null)
        {
            throw new ArgumentNullException(nameof(pathValue));
        }

        return pathValue.Replace('\\', '/');
    }

    /// <summary> Trims trailing forward or backslash directory separators. </summary>
    /// <param name="pathValue"> The path text value. </param>
    /// <returns> The path text without trailing directory separators. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pathValue" /> is <see langword="null" />. </exception>
    public static string TrimTrailingDirectorySeparators (string pathValue)
    {
        if (pathValue == null)
        {
            throw new ArgumentNullException(nameof(pathValue));
        }

        var length = pathValue.Length;
        while (length > 0 && IsDirectorySeparator(pathValue[length - 1]))
        {
            length--;
        }

        return length == pathValue.Length ? pathValue : pathValue[..length];
    }

    private static bool IsDirectorySeparator (char value)
    {
        return value == '/' || value == '\\';
    }
}
