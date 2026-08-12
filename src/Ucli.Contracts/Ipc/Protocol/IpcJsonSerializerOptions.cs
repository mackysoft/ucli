using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Provides shared JSON serializer options for IPC envelopes. </summary>
public static class IpcJsonSerializerOptions
{
    /// <summary> Gets the default serializer options used by IPC client and server components. </summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        Converters =
        {
            new IpcEvalResponseJsonConverter(),
            new VocabularyJsonConverterFactory(),
            new UcliStringValueJsonConverterFactory(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = IpcJsonTypeInfoResolver.Default,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
    };

    /// <summary> Gets serializer options for operation args whose property names must match the published schema exactly. </summary>
    public static JsonSerializerOptions StrictPropertyNames { get; } = new(Default)
    {
        PropertyNameCaseInsensitive = false,
    };

    /// <summary>
    /// Gets the effective serializer options for public raw operation contracts after request-local alias variants
    /// have been removed.
    /// </summary>
    public static JsonSerializerOptions PublicRawOperationContracts { get; } =
        CreatePublicRawOperationContractOptions();

    private static JsonSerializerOptions CreatePublicRawOperationContractOptions ()
    {
        var options = new JsonSerializerOptions(StrictPropertyNames)
        {
            TypeInfoResolver = IpcJsonTypeInfoResolver.Create(RemoveRequestLocalAliasVariant),
        };
        options.MakeReadOnly();
        return options;
    }

    private static void RemoveRequestLocalAliasVariant (JsonTypeInfo typeInfo)
    {
        var derivedTypes = typeInfo.PolymorphismOptions?.DerivedTypes;
        if (derivedTypes == null)
        {
            return;
        }

        for (var i = derivedTypes.Count - 1; i >= 0; i--)
        {
            if (derivedTypes[i].DerivedType == typeof(UcliAliasReferenceArgs))
            {
                derivedTypes.RemoveAt(i);
            }
        }
    }
}
