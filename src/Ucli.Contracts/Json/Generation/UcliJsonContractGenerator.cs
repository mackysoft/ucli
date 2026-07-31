using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MackySoft.JsonSchema.Generation;
using MackySoft.JsonSchema.Generation.Configuration;
using MackySoft.JsonSchema.Generation.ContractModel;
using MackySoft.JsonSchema.Generation.Diagnostics;
using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.JsonSchema.Generation.Projection;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Json.Metadata;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Json.Generation;

/// <summary>
/// Generates JSON Contract Models and projections from effective serializer contracts supplied by uCLI products.
/// </summary>
public static class UcliJsonContractGenerator
{
    private static readonly JsonContractGenerator Generator =
        CreateGenerator(
            JsonContractMetadataProfile.None,
            lifecycleExecutionKind: null,
            commandResultStatus: null);

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

    /// <summary>
    /// Generates one action-specific Lifecycle Execution CLI output contract.
    /// </summary>
    /// <param name="contractId"> The product-assigned stable contract identifier. </param>
    /// <param name="typeInfo"> The effective serializer contract used by the product at runtime. </param>
    /// <param name="documentOptions"> The product-owned projection options. </param>
    /// <param name="executionKind"> The action fixed by the output contract. </param>
    /// <param name="status"> The command result branch represented by the output contract. </param>
    /// <returns>
    /// The provider result with action identity, states, and typed result vocabulary
    /// constrained for the selected command-result branch.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="executionKind" /> or <paramref name="status" /> is undefined.
    /// </exception>
    /// <exception cref="JsonContractGenerationException">
    /// The serializer contract cannot be interpreted without violating the provider generation contract.
    /// </exception>
    internal static JsonContractGenerationResult GenerateWithLifecycleExecutionCliOutputProfile (
        string contractId,
        JsonTypeInfo typeInfo,
        JsonSchemaDocumentOptions documentOptions,
        LifecycleExecutionKind executionKind,
        CommandResultStatus status)
    {
        if (!TextVocabulary.IsDefined(executionKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(executionKind),
                executionKind,
                "Lifecycle Execution kind must be defined.");
        }
        if (!TextVocabulary.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Command result status must be defined.");
        }

        return CreateGenerator(
            JsonContractMetadataProfile.LifecycleExecutionCliOutput,
            executionKind,
            status).Generate(
            new JsonContractGenerationRequest(
                contractId,
                typeInfo,
                documentOptions));
    }

    /// <summary>
    /// Generates one CLI output contract that contains operation execution
    /// application-state declarations.
    /// </summary>
    internal static JsonContractGenerationResult
        GenerateWithOperationExecutionCliOutputProfile (
            string contractId,
            JsonTypeInfo typeInfo,
            JsonSchemaDocumentOptions documentOptions)
    {
        return CreateGenerator(
            JsonContractMetadataProfile.OperationExecutionCliOutput,
            lifecycleExecutionKind: null,
            commandResultStatus: null).Generate(
            new JsonContractGenerationRequest(
                contractId,
                typeInfo,
                documentOptions));
    }

    /// <summary>
    /// Generates the common ExecutionRef contract with its unrestricted feature-state vocabulary.
    /// </summary>
    internal static JsonContractGenerationResult
        GenerateWithExecutionReferenceProfile (
            string contractId,
            JsonTypeInfo typeInfo,
            JsonSchemaDocumentOptions documentOptions)
    {
        return CreateGenerator(
            JsonContractMetadataProfile.ExecutionReference,
            lifecycleExecutionKind: null,
            commandResultStatus: null).Generate(
            new JsonContractGenerationRequest(
                contractId,
                typeInfo,
                documentOptions));
    }

    /// <summary>
    /// Generates the common Lifecycle Execution Terminal Record contract with action-owned constraints.
    /// </summary>
    internal static JsonContractGenerationResult
        GenerateWithLifecycleExecutionTerminalRecordProfile (
            string contractId,
            JsonTypeInfo typeInfo,
            JsonSchemaDocumentOptions documentOptions)
    {
        return CreateGenerator(
            JsonContractMetadataProfile.TerminalRecord,
            lifecycleExecutionKind: null,
            commandResultStatus: null).Generate(
            new JsonContractGenerationRequest(
                contractId,
                typeInfo,
                documentOptions));
    }

    private static JsonContractGenerator CreateGenerator (
        JsonContractMetadataProfile profile,
        LifecycleExecutionKind? lifecycleExecutionKind,
        CommandResultStatus? commandResultStatus)
    {
        var metadataRegistry = CreateMetadataRegistry(
            profile,
            lifecycleExecutionKind,
            commandResultStatus);

        return new JsonContractGenerator(
            new JsonContractGeneratorOptions(
                JsonContractGenerationSettings.ClosedObjects,
                metadataRegistry,
                CreateTypeMappers(),
                modelContributors:
                [
                    new UcliSemanticAnnotationContractModelContributor(),
                ]));
    }

    private static JsonContractMetadataRegistry CreateMetadataRegistry (
        JsonContractMetadataProfile profile,
        LifecycleExecutionKind? lifecycleExecutionKind,
        CommandResultStatus? commandResultStatus)
    {
        var registry = new JsonContractMetadataRegistry()
            .RegisterAttributeInterpreter<UcliInt32MinimumAttribute, int?>(
                new UcliNullableInt32MinimumAttributeInterpreter())
            .RegisterAttributeInterpreter<UcliInt32RangeAttribute, int?>(
                new UcliNullableInt32RangeAttributeInterpreter());
        if (profile == JsonContractMetadataProfile.None)
        {
            if (lifecycleExecutionKind.HasValue
                || commandResultStatus.HasValue)
            {
                throw new ArgumentException(
                    "The default JSON contract profile cannot include Lifecycle Execution selectors.",
                    nameof(lifecycleExecutionKind));
            }

            return registry;
        }

        if (profile == JsonContractMetadataProfile.OperationExecutionCliOutput)
        {
            EnsureLifecycleExecutionSelectorsAreAbsent(
                lifecycleExecutionKind,
                commandResultStatus,
                "The operation execution CLI output profile cannot include Lifecycle Execution selectors.");
            RegisterOperationApplicationState(registry);
            return registry;
        }

        if (profile
            == JsonContractMetadataProfile.ExecutionReference)
        {
            if (lifecycleExecutionKind.HasValue
                || commandResultStatus.HasValue)
            {
                throw new ArgumentException(
                    "The ExecutionRef profile cannot include action selectors.",
                    nameof(lifecycleExecutionKind));
            }

            LifecycleExecutionCliOutputMetadata.RegisterDefaultExecutionState(
                registry);
            return registry;
        }

        if (profile
            == JsonContractMetadataProfile.TerminalRecord)
        {
            if (lifecycleExecutionKind.HasValue
                || commandResultStatus.HasValue)
            {
                throw new ArgumentException(
                    "The Terminal Record profile cannot include CLI action selectors.",
                    nameof(lifecycleExecutionKind));
            }

            LifecycleExecutionTerminalRecordMetadata.Register(registry);
            return registry;
        }

        if (!lifecycleExecutionKind.HasValue
            || !commandResultStatus.HasValue)
        {
            throw new ArgumentException(
                "A Lifecycle Execution CLI output profile requires an action kind and command result status.",
                nameof(lifecycleExecutionKind));
        }

        RegisterOperationApplicationState(registry);
        LifecycleExecutionCliOutputMetadata.Register(
            registry,
            lifecycleExecutionKind.Value,
            commandResultStatus.Value);
        return registry;
    }

    private static void RegisterOperationApplicationState (
        JsonContractMetadataRegistry registry)
    {
        registry.RegisterAttributeInterpreter<
            UcliOperationApplicationStateAttribute,
            ExecutionApplicationState>(
            new UcliOperationApplicationStateAttributeInterpreter());
    }

    private static void EnsureLifecycleExecutionSelectorsAreAbsent (
        LifecycleExecutionKind? lifecycleExecutionKind,
        CommandResultStatus? commandResultStatus,
        string message)
    {
        if (lifecycleExecutionKind.HasValue
            || commandResultStatus.HasValue)
        {
            throw new ArgumentException(
                message,
                nameof(lifecycleExecutionKind));
        }
    }

    private enum JsonContractMetadataProfile
    {
        None = 0,

        ExecutionReference = 1,

        TerminalRecord = 2,

        LifecycleExecutionCliOutput = 3,

        OperationExecutionCliOutput = 4,
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
