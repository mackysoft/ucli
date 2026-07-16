using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Features.Assurance;

/// <summary> Identifies one assurance report by exactly one path or URI locator. </summary>
internal sealed record AssuranceReportReference
{
    private AssuranceReportReference (
        string? path,
        string? uri,
        Sha256Digest? digest)
    {
        Path = path;
        Uri = uri;
        Digest = digest;
    }

    /// <summary> Gets the path locator, or <see langword="null" /> when this reference uses a URI. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; }

    /// <summary> Gets the URI locator, or <see langword="null" /> when this reference uses a path. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Uri { get; }

    /// <summary> Gets the canonical SHA-256 digest, or <see langword="null" /> when integrity metadata is unavailable. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Sha256Digest? Digest { get; }

    /// <summary> Creates a report reference with a path as its only locator. </summary>
    /// <param name="path"> The non-empty path without leading or trailing whitespace. </param>
    /// <param name="digest"> The canonical SHA-256 digest, or <see langword="null" /> when integrity metadata is unavailable. </param>
    /// <returns> A report reference whose <see cref="Path" /> is set and whose <see cref="Uri" /> is <see langword="null" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="path" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="path" /> is empty, whitespace-only, or has leading or trailing whitespace. </exception>
    public static AssuranceReportReference FromPath (
        string path,
        Sha256Digest? digest)
    {
        return new AssuranceReportReference(
            path: ValidateLocator(path, nameof(path)),
            uri: null,
            digest);
    }

    /// <summary> Creates a report reference with a URI as its only locator. </summary>
    /// <param name="uri"> The non-empty URI text without leading or trailing whitespace. </param>
    /// <param name="digest"> The canonical SHA-256 digest, or <see langword="null" /> when integrity metadata is unavailable. </param>
    /// <returns> A report reference whose <see cref="Uri" /> is set and whose <see cref="Path" /> is <see langword="null" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="uri" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="uri" /> is empty, whitespace-only, or has leading or trailing whitespace. </exception>
    public static AssuranceReportReference FromUri (
        string uri,
        Sha256Digest? digest)
    {
        return new AssuranceReportReference(
            path: null,
            uri: ValidateLocator(uri, nameof(uri)),
            digest);
    }

    private static string ValidateLocator (
        string value,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
        {
            throw new ArgumentException(
                "Report locator must not have leading or trailing whitespace.",
                parameterName);
        }

        return value;
    }
}
