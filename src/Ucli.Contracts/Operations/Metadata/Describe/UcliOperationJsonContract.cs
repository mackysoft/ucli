using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary>
/// Carries the unmodified JSON Schema and type-metadata projections generated for one operation value contract.
/// </summary>
public readonly record struct UcliOperationJsonContract
{
    /// <summary> Initializes one typed generated-operation contract. </summary>
    /// <param name="contractDigest"> The authoritative Contract Model digest. </param>
    /// <param name="typeMetadata"> The provider type-metadata object. </param>
    /// <param name="schema"> The provider JSON Schema object. </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="contractDigest" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="typeMetadata" /> or <paramref name="schema" /> is not initialized.
    /// </exception>
    [JsonConstructor]
    public UcliOperationJsonContract (
        Sha256Digest contractDigest,
        UcliJsonObject typeMetadata,
        UcliJsonObject schema)
    {
        ContractDigest = contractDigest
            ?? throw new ArgumentNullException(nameof(contractDigest));
        TypeMetadata = typeMetadata.IsDefined
            ? typeMetadata
            : throw new ArgumentException(
                "Type metadata must contain a JSON object.",
                nameof(typeMetadata));
        Schema = schema.IsDefined
            ? schema
            : throw new ArgumentException(
                "Schema must contain a JSON object.",
                nameof(schema));
    }

    /// <summary> Initializes one public operation value contract from a provider generation result. </summary>
    /// <param name="generationResult">
    /// The authoritative generation result that supplies the digest and both projections.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generationResult" /> is <see langword="null" />.
    /// </exception>
    internal UcliOperationJsonContract (JsonContractGenerationResult generationResult)
        : this(
            ParseDigest(generationResult),
            ParseProjection(generationResult, typeMetadata: true),
            ParseProjection(generationResult, typeMetadata: false))
    {
    }

    /// <summary> Gets the digest of the authoritative JSON Contract Model. </summary>
    [JsonRequired]
    public Sha256Digest ContractDigest { get; init; }

    /// <summary> Gets the provider type-metadata projection. </summary>
    [JsonRequired]
    public UcliJsonObject TypeMetadata { get; init; }

    /// <summary> Gets the provider JSON Schema projection. </summary>
    [JsonRequired]
    public UcliJsonObject Schema { get; init; }

    /// <summary> Gets a value indicating whether all three generated values are initialized. </summary>
    [JsonIgnore]
    public bool IsDefined =>
        ContractDigest is not null
        && TypeMetadata.IsDefined
        && Schema.IsDefined;

    private static Sha256Digest ParseDigest (
        JsonContractGenerationResult? generationResult)
    {
        if (generationResult == null)
        {
            throw new ArgumentNullException(nameof(generationResult));
        }

        return Sha256Digest.Parse(generationResult.ContractDigest);
    }

    private static UcliJsonObject ParseProjection (
        JsonContractGenerationResult? generationResult,
        bool typeMetadata)
    {
        if (generationResult == null)
        {
            throw new ArgumentNullException(nameof(generationResult));
        }

        var utf8Json = typeMetadata
            ? generationResult.GetTypeMetadataUtf8()
            : generationResult.GetJsonSchemaUtf8();
        using var document = JsonDocument.Parse(utf8Json);
        return new UcliJsonObject(document.RootElement);
    }
}
