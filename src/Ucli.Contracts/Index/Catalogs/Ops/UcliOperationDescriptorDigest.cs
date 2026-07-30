using System.Text.Json;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Calculates the stable identity of one semantic operation descriptor. </summary>
internal static class UcliOperationDescriptorDigest
{
    /// <summary>
    /// Calculates an RFC 8785 canonical SHA-256 digest from every public descriptor JSON field except
    /// <c>descriptorDigest</c> itself.
    /// </summary>
    /// <param name="descriptor"> The operation descriptor. </param>
    /// <returns> The calculated descriptor digest. </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="descriptor" /> is <see langword="null" />.
    /// </exception>
    internal static Sha256Digest Calculate (IndexOpEntryJsonContract descriptor)
    {
        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            IndexOpEntryJsonContractWriter.WriteDescriptorDigestInput(writer, descriptor);
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return Sha256Digest.Compute(
            Rfc8785JsonCanonicalizer.Canonicalize(document.RootElement));
    }
}
