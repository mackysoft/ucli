namespace MackySoft.Ucli.Skills.Shared;

/// <summary> Represents one deterministic text file in a SKILL package. </summary>
/// <param name="RelativePath"> The slash-separated package-relative path. </param>
/// <param name="Content"> The UTF-8 text content normalized to LF line endings. </param>
public sealed record SkillPackageFile (
    string RelativePath,
    string Content)
{
    /// <summary> Creates one package file after validating path and content. </summary>
    /// <param name="relativePath"> The slash-separated package-relative path. </param>
    /// <param name="content"> The file content. </param>
    /// <returns> The package file. </returns>
    public static SkillPackageFile Create (
        string relativePath,
        string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);

        if (relativePath.Contains('\\', StringComparison.Ordinal)
            || relativePath.StartsWith("/", StringComparison.Ordinal)
            || relativePath.Split('/').Any(static segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw new ArgumentException("Package file path must be a safe slash-separated relative path.", nameof(relativePath));
        }

        return new SkillPackageFile(relativePath, SkillTextNormalizer.NormalizeToLf(content));
    }
}
