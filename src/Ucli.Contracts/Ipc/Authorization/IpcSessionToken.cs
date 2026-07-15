using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc.Authorization;

/// <summary> Represents one canonical IPC session token without exposing its encoded value through general string conversion. </summary>
public sealed class IpcSessionToken : IEquatable<IpcSessionToken>
{
    private const int RandomByteCount = 32;

    private const int EncodedCharacterCount = 43;

    private const string RedactedText = "[REDACTED]";

    private readonly string encodedValue;

    private IpcSessionToken (string encodedValue)
    {
        this.encodedValue = encodedValue;
    }

    /// <summary> Creates a token from 32 cryptographically secure random bytes. </summary>
    /// <returns> A token encoded as exactly 43 unpadded canonical base64url characters. </returns>
    public static IpcSessionToken CreateRandom ()
    {
        Span<byte> randomBytes = stackalloc byte[RandomByteCount];
        RandomNumberGenerator.Fill(randomBytes);
        return new IpcSessionToken(Base64UrlCodec.Encode(randomBytes));
    }

    /// <summary> Attempts to parse one canonical encoded token. </summary>
    /// <param name="encodedValue"> The candidate encoded token. </param>
    /// <param name="token"> The token when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the value is exactly 43 unpadded canonical base64url characters; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? encodedValue,
        [NotNullWhen(true)] out IpcSessionToken? token)
    {
        token = null;
        if (encodedValue is not { Length: EncodedCharacterCount }
            || !Base64UrlCodec.IsCanonical(encodedValue))
        {
            return false;
        }

        token = new IpcSessionToken(encodedValue);
        return true;
    }

    /// <summary> Gets the canonical encoded value for an explicit IPC or persistence boundary. </summary>
    /// <returns> The 43-character canonical base64url value. </returns>
    public string GetEncodedValue ()
    {
        return encodedValue;
    }

    /// <inheritdoc />
    public bool Equals (IpcSessionToken? other)
    {
        return other is not null
            && FixedTimeEquals(encodedValue, other.encodedValue);
    }

    /// <inheritdoc />
    public override bool Equals (object? obj)
    {
        return obj is IpcSessionToken other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode ()
    {
        return StringComparer.Ordinal.GetHashCode(encodedValue);
    }

    /// <summary> Determines whether two tokens have the same encoded value. </summary>
    public static bool operator == (IpcSessionToken? left, IpcSessionToken? right)
    {
        return left is null
            ? right is null
            : left.Equals(right);
    }

    /// <summary> Determines whether two tokens have different encoded values. </summary>
    public static bool operator != (IpcSessionToken? left, IpcSessionToken? right)
    {
        return !(left == right);
    }

    /// <summary> Returns a redacted representation that never contains the encoded token. </summary>
    public override string ToString ()
    {
        return RedactedText;
    }

    private static bool FixedTimeEquals (string left, string right)
    {
        return CryptographicOperations.FixedTimeEquals(
            MemoryMarshal.AsBytes(left.AsSpan()),
            MemoryMarshal.AsBytes(right.AsSpan()));
    }

}
