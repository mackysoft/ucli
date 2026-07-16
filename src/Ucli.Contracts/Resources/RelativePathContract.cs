using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Defines shared syntax rules for portable relative paths carried by uCLI contracts. </summary>
public static class RelativePathContract
{
    /// <summary> Normalizes and validates one portable relative path. </summary>
    /// <param name="path"> The input path. </param>
    /// <param name="normalizedPath"> The slash-separated normalized path when validation succeeds. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is relative and does not contain empty, <c>.</c>, or <c>..</c> segments; otherwise <see langword="false" />. </returns>
    public static bool TryNormalize (
        string? path,
        out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path)
            || StringValueValidator.HasOuterWhitespace(path))
        {
            return false;
        }

        var candidate = PortablePathText.ToSlashSeparated(path);
        if (!IsRelativePathSyntax(candidate))
        {
            return false;
        }

        normalizedPath = candidate;
        return true;
    }

    /// <summary> Determines whether <paramref name="path" /> is already normalized portable relative path text. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> satisfies normalized relative path syntax; otherwise <see langword="false" />. </returns>
    public static bool IsNormalized ([NotNullWhen(true)] string? path)
    {
        return path != null
            && !path.Contains('\\', StringComparison.Ordinal)
            && TryNormalize(path, out var normalizedPath)
            && string.Equals(path, normalizedPath, StringComparison.Ordinal);
    }

    private static bool IsRelativePathSyntax (string path)
    {
        return path.Length > 0
            && !path.StartsWith("/", StringComparison.Ordinal)
            && !path.Contains(':', StringComparison.Ordinal)
            && !StringValueValidator.HasControlCharacterOrMalformedUtf16(path)
            && HasValidSegments(path);
    }

    private static bool HasValidSegments (string path)
    {
        var segmentStartIndex = 0;
        for (var i = 0; i <= path.Length; i++)
        {
            if (i < path.Length && path[i] != '/')
            {
                continue;
            }

            var segmentLength = i - segmentStartIndex;
            if (segmentLength == 0
                || IsCurrentDirectorySegment(path, segmentStartIndex, segmentLength)
                || IsParentDirectorySegment(path, segmentStartIndex, segmentLength))
            {
                return false;
            }

            segmentStartIndex = i + 1;
        }

        return true;
    }

    private static bool IsCurrentDirectorySegment (
        string path,
        int segmentStartIndex,
        int segmentLength)
    {
        return segmentLength == 1
            && path[segmentStartIndex] == '.';
    }

    private static bool IsParentDirectorySegment (
        string path,
        int segmentStartIndex,
        int segmentLength)
    {
        return segmentLength == 2
            && path[segmentStartIndex] == '.'
            && path[segmentStartIndex + 1] == '.';
    }
}
