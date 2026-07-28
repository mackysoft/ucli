using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Hosting.Cli.Common.Contracts;

/// <summary> Provides JSON serializer options for public CLI output contracts. </summary>
internal static class CliOutputJsonSerializerOptions
{
    /// <summary> Gets the serializer options shared by command results and stream entries. </summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        Converters =
        {
            new UcliNonNullJsonObjectJsonConverterFactory(),
            new VocabularyJsonConverterFactory(),
            new MackySoft.AgentSkills.Shared.Text.ContractLiteralJsonConverterFactory(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = CreateTypeInfoResolver(),
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>
    /// Creates metadata for the write-only public CLI contract.
    /// The same metadata drives serialization and Schema generation so that an
    /// unconditionally written property is also required by the generated Schema.
    /// </summary>
    private static DefaultJsonTypeInfoResolver CreateTypeInfoResolver ()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(static typeInfo =>
        {
            ArtifactRefJsonPolymorphismConfigurator.TryConfigure(typeInfo);
            ExecutionRefJsonPolymorphismConfigurator.TryConfigure(typeInfo);
        });
        resolver.Modifiers.Add(ConfigureCommandErrorPayload);
        resolver.Modifiers.Add(MarkAlwaysWrittenPropertiesRequired);
        return resolver;
    }

    private static void ConfigureCommandErrorPayload (JsonTypeInfo typeInfo)
    {
        var contractType = typeInfo.Type;
        if (!contractType.IsGenericType
            || contractType.GetGenericTypeDefinition() != typeof(CommandErrorPayload<>))
        {
            return;
        }

        var detailsType = contractType.GetGenericArguments()[0];
        var emptyType = typeof(EmptyCommandErrorPayload<>).MakeGenericType(detailsType);
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "payloadKind",
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        options.DerivedTypes.Add(new JsonDerivedType(
            emptyType,
            TextVocabulary.GetText(CommandErrorPayloadKind.Empty)));
        options.DerivedTypes.Add(new JsonDerivedType(
            detailsType,
            TextVocabulary.GetText(CommandErrorPayloadKind.Detailed)));
        typeInfo.PolymorphismOptions = options;
    }

    /// <summary>
    /// Marks properties without a serialization condition as required.
    /// System.Text.Json requires a setter before it accepts required metadata, including
    /// for output DTOs whose public contract is intentionally getter-only. The throwing
    /// setter makes this output-only boundary explicit without changing the DTO surface.
    /// </summary>
    private static void MarkAlwaysWrittenPropertiesRequired (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (var property in typeInfo.Properties)
        {
            if (property.Get != null
                && !property.IsExtensionData
                && property.ShouldSerialize == null)
            {
                property.Set ??= ThrowOutputContractDeserialization;
                property.IsRequired = true;
            }
        }
    }

    private static void ThrowOutputContractDeserialization (
        object _,
        object? __)
    {
        throw new NotSupportedException(
            "CLI output serializer metadata does not support deserialization.");
    }
}
