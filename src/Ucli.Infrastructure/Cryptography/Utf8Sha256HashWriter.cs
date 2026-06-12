using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Cryptography;

/// <summary> Incrementally computes a SHA-256 digest from UTF-8 encoded text fragments. </summary>
internal sealed class Utf8Sha256HashWriter : IDisposable
{
    private const int CharChunkSize = 256;
    private const int ByteChunkSize = 1024;

    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    private bool disposed;

    /// <summary> Appends one UTF-8 text fragment to the hash input. </summary>
    public void Append (string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Append(value.AsSpan());
    }

    /// <summary> Appends one UTF-8 character to the hash input. </summary>
    public void Append (char value)
    {
        Append(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
    }

    /// <summary> Completes the current digest and resets the writer for another hash. </summary>
    public string GetHashAndReset ()
    {
        ThrowIfDisposed();
        return Sha256LowerHex.GetHashAndReset(hash);
    }

    /// <inheritdoc />
    public void Dispose ()
    {
        if (disposed)
        {
            return;
        }

        hash.Dispose();
        disposed = true;
    }

    private void Append (ReadOnlySpan<char> value)
    {
        ThrowIfDisposed();
        Span<byte> bytes = stackalloc byte[ByteChunkSize];
        while (!value.IsEmpty)
        {
            var charCount = Math.Min(value.Length, CharChunkSize);
            if (charCount < value.Length
                && char.IsHighSurrogate(value[charCount - 1])
                && char.IsLowSurrogate(value[charCount]))
            {
                charCount--;
            }

            var chars = value[..charCount];
            var byteCount = Encoding.UTF8.GetBytes(chars, bytes);
            hash.AppendData(bytes[..byteCount]);
            value = value[charCount..];
        }
    }

    private void ThrowIfDisposed ()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(Utf8Sha256HashWriter));
        }
    }
}
