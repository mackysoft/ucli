using System.Text.Json;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Calculates the stable RFC 8785 identity of a Lifecycle Execution definition. </summary>
public static class LifecycleExecutionDefinitionDigest
{
    /// <summary> Calculates the SHA-256 digest of the definition's canonical JSON bytes. </summary>
    /// <param name="definition"> The typed definition fixed before execution registration. </param>
    /// <returns> The definition digest used by the corresponding <see cref="ExecutionRef" />. </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="definition" /> is <see langword="null" />.
    /// </exception>
    public static Sha256Digest Calculate (LifecycleExecutionDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString(
                JsonNamingPolicy.CamelCase.ConvertName(
                    nameof(LifecycleExecutionDefinition.Kind)),
                TextVocabulary.GetText(definition.Kind));
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return Sha256Digest.Compute(
            Rfc8785JsonCanonicalizer.Canonicalize(document.RootElement));
    }
}
