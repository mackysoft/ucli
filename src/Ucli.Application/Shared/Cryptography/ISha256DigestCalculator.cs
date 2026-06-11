namespace MackySoft.Ucli.Application.Shared.Cryptography;

/// <summary> Computes SHA-256 digests as lowercase hexadecimal text. </summary>
internal interface ISha256DigestCalculator
{
    /// <summary> Computes a SHA-256 digest string from source bytes. </summary>
    /// <param name="bytes"> The source bytes. </param>
    /// <returns> The lowercase hexadecimal SHA-256 digest string. </returns>
    string Compute (ReadOnlySpan<byte> bytes);
}
