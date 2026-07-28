using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MackySoft.JsonSchema.Generation;
using MackySoft.JsonSchema.Generation.Configuration;
using MackySoft.JsonSchema.Generation.ContractModel;
using MackySoft.JsonSchema.Generation.Diagnostics;
using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.JsonSchema.Generation.Projection;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Json.Metadata;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Json.Generation;

/// <summary>
/// Generates JSON Contract Models and projections from effective serializer contracts supplied by uCLI products.
/// </summary>
public static class UcliJsonContractGenerator
{
    private static readonly JsonContractGenerator Generator = CreateGenerator();

    /// <summary>
    /// Generates one provider result from an authoritative serializer type resolver and product-owned document options.
    /// </summary>
    /// <param name="contractId"> The product-assigned stable contract identifier. </param>
    /// <param name="typeInfo"> The effective serializer contract used by the product at runtime. </param>
    /// <param name="documentOptions"> The product-owned projection options. </param>
    /// <returns>
    /// The provider result that contains one immutable Contract Model and both deterministic projections.
    /// </returns>
    /// <exception cref="ArgumentNullException"> A required argument is <see langword="null" />. </exception>
    /// <exception cref="JsonContractGenerationException">
    /// The serializer contract cannot be interpreted without violating the provider generation contract.
    /// </exception>
    public static JsonContractGenerationResult Generate (
        string contractId,
        JsonTypeInfo typeInfo,
        JsonSchemaDocumentOptions documentOptions)
    {
        return Generator.Generate(new JsonContractGenerationRequest(
            contractId,
            typeInfo,
            documentOptions));
    }

    /// <summary>
    /// Generates one deterministic provider result set with uCLI's fixed serializer semantics.
    /// </summary>
    /// <param name="requests"> The complete finite set of product-owned contract inputs. </param>
    /// <returns> Provider results ordered by contract identifier. </returns>
    /// <exception cref="ArgumentNullException"> <paramref name="requests" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> <paramref name="requests" /> contains <see langword="null" />. </exception>
    /// <exception cref="JsonContractGenerationException">
    /// A contract identifier is invalid or duplicated, or a serializer contract cannot be generated.
    /// </exception>
    public static IReadOnlyList<JsonContractGenerationResult> GenerateSet (
        IEnumerable<JsonContractGenerationRequest> requests)
    {
        return Generator.GenerateSet(requests);
    }

    private static JsonContractGenerator CreateGenerator ()
    {
        return new JsonContractGenerator(
            new JsonContractGeneratorOptions(
                JsonContractGenerationSettings.ClosedObjects,
                CreateMetadataRegistry(),
                CreateTypeMappers(),
                modelContributors:
                [
                    new UcliSemanticAnnotationContractModelContributor(),
                ]));
    }

    private static JsonContractMetadataRegistry CreateMetadataRegistry ()
    {
        return new JsonContractMetadataRegistry()
            .RegisterAttributeInterpreter<UcliInt32MinimumAttribute, int?>(
                new UcliNullableInt32MinimumAttributeInterpreter())
            .RegisterAttributeInterpreter<UcliInt32RangeAttribute, int?>(
                new UcliNullableInt32RangeAttributeInterpreter());
    }

    private static IReadOnlyList<IJsonContractTypeMapper> CreateTypeMappers ()
    {
        return
        [
            new UcliNonNullJsonObjectJsonContractTypeMapper(),
            new UcliJsonObjectJsonContractTypeMapper(),
            new UcliVocabularyJsonContractTypeMapper(),
            new UcliStringValueJsonContractTypeMapper(),
            CreateSha256DigestMapper(),
            CreateCodeMapper(),
            CreateProjectFingerprintMapper(),
            CreateDateTimeOffsetMapper(),
            CreateArtifactPublicationTimeMapper(),
        ];
    }

    private static IJsonContractTypeMapper CreateSha256DigestMapper ()
    {
        return CreateStringScalarMapper(
            "ucli.sha256-digest",
            typeof(Sha256Digest),
            typeof(Sha256DigestJsonConverter));
    }

    private static IJsonContractTypeMapper CreateCodeMapper ()
    {
        return CreateStringScalarMapper(
            "ucli.code",
            typeof(UcliCode),
            typeof(UcliCodeJsonConverter));
    }

    private static IJsonContractTypeMapper CreateProjectFingerprintMapper ()
    {
        return CreateStringScalarMapper(
            "ucli.project-fingerprint",
            typeof(ProjectFingerprint),
            typeof(ProjectFingerprintJsonConverter));
    }

    private static IJsonContractTypeMapper CreateDateTimeOffsetMapper ()
    {
        var converterType = JsonSerializerOptions.Default
            .GetTypeInfo(typeof(DateTimeOffset))
            .Converter
            .GetType();
        return CreateStringScalarMapper(
            "ucli.stj-date-time-offset",
            typeof(DateTimeOffset),
            converterType);
    }

    private static IJsonContractTypeMapper CreateArtifactPublicationTimeMapper ()
    {
        return CreateStringScalarMapper(
            "ucli.artifact-publication-time",
            typeof(DateTimeOffset),
            typeof(ArtifactPublicationTimeJsonConverter));
    }

    private static IJsonContractTypeMapper CreateStringScalarMapper (
        string stableId,
        Type valueType,
        Type converterType)
    {
        return new UcliExactConverterScalarJsonContractTypeMapper(
            stableId,
            valueType,
            converterType,
            JsonContractScalarKind.String);
    }
}
