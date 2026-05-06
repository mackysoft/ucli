namespace MackySoft.Ucli.Skills.Shared;

/// <summary> Validates slash-separated relative paths and path segments used by SKILL files. </summary>
internal static class SkillRelativePath
{
    /// <summary> Returns whether a path is safe as a slash-separated relative file path. </summary>
    /// <param name="relativePath"> The relative path to inspect. </param>
    /// <returns> <see langword="true" /> when the path is a safe relative file path. </returns>
    public static bool IsSafeFilePath (string? relativePath)
    {
        return !string.IsNullOrWhiteSpace(relativePath)
            && !Path.IsPathRooted(relativePath)
            && !relativePath.Contains('\\', StringComparison.Ordinal)
            && relativePath.Split('/').All(static segment => IsSafePathSegment(segment));
    }

    /// <summary> Returns whether a path segment is safe inside a slash-separated relative path. </summary>
    /// <param name="segment"> The path segment to inspect. </param>
    /// <returns> <see langword="true" /> when the segment is safe inside a relative path. </returns>
    public static bool IsSafePathSegment (string? segment)
    {
        return !string.IsNullOrWhiteSpace(segment)
            && segment is not "." and not ".."
            && !Path.IsPathRooted(segment)
            && !segment.Contains('/', StringComparison.Ordinal)
            && !segment.Contains('\\', StringComparison.Ordinal);
    }
}
