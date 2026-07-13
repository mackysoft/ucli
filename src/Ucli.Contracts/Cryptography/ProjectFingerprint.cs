using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts;

/// <summary> Represents one project fingerprint as exactly 64 lowercase hexadecimal SHA-256 characters. </summary>
[JsonConverter(typeof(ProjectFingerprintJsonConverter))]
public sealed class ProjectFingerprint : IEquatable<ProjectFingerprint>
{
    private readonly string value;

    /// <summary> Initializes one canonical project fingerprint. </summary>
    /// <param name="value"> The 64-character lowercase hexadecimal SHA-256 value. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is not exactly 64 lowercase hexadecimal characters. </exception>
    public ProjectFingerprint (string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!Sha256LowerHex.IsLowerHexDigest(value))
        {
            throw new ArgumentException(
                "Project fingerprint must be exactly 64 lowercase hexadecimal SHA-256 characters.",
                nameof(value));
        }

        this.value = value;
    }

    /// <summary> Attempts to parse one canonical project fingerprint. </summary>
    /// <param name="value"> The candidate fingerprint text. </param>
    /// <param name="fingerprint"> The parsed fingerprint when successful; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is exactly 64 lowercase hexadecimal characters; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out ProjectFingerprint? fingerprint)
    {
        fingerprint = null;
        if (!Sha256LowerHex.IsLowerHexDigest(value))
        {
            return false;
        }

        fingerprint = new ProjectFingerprint(value);
        return true;
    }

    /// <inheritdoc />
    public bool Equals (ProjectFingerprint? other)
    {
        return ReferenceEquals(this, other)
            || (other is not null && string.Equals(value, other.value, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public override bool Equals (object? obj)
    {
        return obj is ProjectFingerprint other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode ()
    {
        return StringComparer.Ordinal.GetHashCode(value);
    }

    /// <summary> Determines whether two project fingerprints have the same canonical value. </summary>
    public static bool operator == (ProjectFingerprint? left, ProjectFingerprint? right)
    {
        return left is null
            ? right is null
            : left.Equals(right);
    }

    /// <summary> Determines whether two project fingerprints have different canonical values. </summary>
    public static bool operator != (ProjectFingerprint? left, ProjectFingerprint? right)
    {
        return !(left == right);
    }

    /// <summary> Returns the canonical lowercase hexadecimal project fingerprint. </summary>
    public override string ToString ()
    {
        return value;
    }
}
