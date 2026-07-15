using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Cryptography;

/// <summary> Represents one canonical lowercase hexadecimal SHA-256 digest. </summary>
[JsonConverter(typeof(Sha256DigestJsonConverter))]
public sealed class Sha256Digest : IEquatable<Sha256Digest>
{
    private readonly string value;

    private Sha256Digest (string value)
    {
        this.value = value;
    }

    internal static Sha256Digest FromHashBytes (ReadOnlySpan<byte> hashBytes)
    {
        return new Sha256Digest(Sha256LowerHex.ToLowerHex(hashBytes));
    }

    /// <summary> Computes a digest from source bytes. </summary>
    /// <param name="bytes"> The source bytes. </param>
    /// <returns> The computed digest. </returns>
    public static Sha256Digest Compute (ReadOnlySpan<byte> bytes)
    {
        return new Sha256Digest(Sha256LowerHex.Compute(bytes));
    }

    /// <summary> Parses canonical digest text. </summary>
    /// <param name="value"> The candidate digest text. </param>
    /// <returns> The parsed digest. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="FormatException"> Thrown when <paramref name="value" /> is not canonical digest text. </exception>
    public static Sha256Digest Parse (string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!TryParse(value, out var digest))
        {
            throw new FormatException("SHA-256 digest must be exactly 64 lowercase hexadecimal characters.");
        }

        return digest;
    }

    /// <summary> Attempts to parse canonical digest text. </summary>
    /// <param name="value"> The candidate digest text. </param>
    /// <param name="digest"> The parsed digest when successful; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is canonical digest text; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out Sha256Digest? digest)
    {
        digest = null;
        if (!Sha256LowerHex.IsLowerHexDigest(value))
        {
            return false;
        }

        digest = new Sha256Digest(value);
        return true;
    }

    /// <inheritdoc />
    public bool Equals (Sha256Digest? other)
    {
        return ReferenceEquals(this, other)
            || (other is not null && string.Equals(value, other.value, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public override bool Equals (object? obj)
    {
        return obj is Sha256Digest other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode ()
    {
        return StringComparer.Ordinal.GetHashCode(value);
    }

    /// <summary> Determines whether two digest values are equal. </summary>
    public static bool operator == (Sha256Digest? left, Sha256Digest? right)
    {
        return left is null
            ? right is null
            : left.Equals(right);
    }

    /// <summary> Determines whether two digest values differ. </summary>
    public static bool operator != (Sha256Digest? left, Sha256Digest? right)
    {
        return !(left == right);
    }

    /// <summary> Returns the canonical lowercase hexadecimal digest text. </summary>
    public override string ToString ()
    {
        return value;
    }
}
