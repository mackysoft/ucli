using System.Text;
using MackySoft.Ucli.Infrastructure.Cryptography;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Digests;

/// <summary> Computes deterministic SKILL content and host artifact digests. </summary>
public sealed class SkillDigestCalculator
{
    private const string DigestPrefix = "sha256:";

    /// <summary> Computes one digest from normalized digest input files. </summary>
    /// <param name="files"> The files included in the digest input. </param>
    /// <returns> The digest in <c>sha256:&lt;lowerhex&gt;</c> form. </returns>
    public string ComputeDigest (IEnumerable<SkillDigestInputFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        using var stream = new MemoryStream();
        foreach (var file in files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
        {
            ValidateRelativePath(file.RelativePath);
            WriteUtf8(stream, file.RelativePath);
            stream.WriteByte(0);
            WriteUtf8(stream, SkillTextNormalizer.NormalizeToLf(file.Content));
        }

        return DigestPrefix + Sha256LowerHex.Compute(stream.ToArray());
    }

    /// <summary> Computes one digest for a single text artifact. </summary>
    /// <param name="relativePath"> The artifact path. </param>
    /// <param name="content"> The artifact content. </param>
    /// <returns> The digest in <c>sha256:&lt;lowerhex&gt;</c> form. </returns>
    public string ComputeSingleFileDigest (
        string relativePath,
        string content)
    {
        return ComputeDigest([new SkillDigestInputFile(relativePath, content)]);
    }

    private static void ValidateRelativePath (string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (relativePath.Contains('\\', StringComparison.Ordinal)
            || relativePath.StartsWith("/", StringComparison.Ordinal)
            || relativePath.Split('/').Any(static segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw new ArgumentException("Digest file path must be a safe slash-separated relative path.", nameof(relativePath));
        }
    }

    private static void WriteUtf8 (
        Stream stream,
        string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
