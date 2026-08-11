using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MackySoft.Ucli.Contracts.Recording;

/// <summary>Configures recording payload and selection tagged unions.</summary>
internal static class GameViewRecordingJsonPolymorphismConfigurator
{
    public static bool TryConfigure (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(GameViewRecordingPayload))
        {
            typeInfo.PolymorphismOptions = CreatePayloadOptions(typeInfo);
            return true;
        }
        if (typeInfo.Type == typeof(GameViewRecordingExecutionPayload))
        {
            typeInfo.PolymorphismOptions = CreateExecutionPayloadOptions(typeInfo);
            return true;
        }
        if (typeInfo.Type == typeof(GameViewRecordingSelection))
        {
            typeInfo.PolymorphismOptions = CreateSelectionOptions(typeInfo);
            return true;
        }
        if (typeInfo.Type == typeof(GameViewRecordingStopResultPayload))
        {
            typeInfo.PolymorphismOptions = CreateStopResultOptions(typeInfo);
            return true;
        }

        return false;
    }

    private static JsonPolymorphismOptions CreatePayloadOptions (JsonTypeInfo typeInfo)
    {
        var options = CreateOptions(typeInfo, "payloadKind");
        options.DerivedTypes.Add(new JsonDerivedType(typeof(GameViewRecordingStatusPayload), "status"));
        options.DerivedTypes.Add(new JsonDerivedType(typeof(GameViewRecordingActivePayload), "active"));
        options.DerivedTypes.Add(new JsonDerivedType(typeof(GameViewRecordingRecoveryPayload), "recovery"));
        options.DerivedTypes.Add(new JsonDerivedType(typeof(GameViewRecordingTerminalPayload), "terminal"));
        return options;
    }

    private static JsonPolymorphismOptions CreateExecutionPayloadOptions (JsonTypeInfo typeInfo)
    {
        var options = CreateOptions(typeInfo, "payloadKind");
        options.DerivedTypes.Add(new JsonDerivedType(typeof(GameViewRecordingActivePayload), "active"));
        options.DerivedTypes.Add(new JsonDerivedType(typeof(GameViewRecordingRecoveryPayload), "recovery"));
        options.DerivedTypes.Add(new JsonDerivedType(typeof(GameViewRecordingTerminalPayload), "terminal"));
        return options;
    }

    private static JsonPolymorphismOptions CreateSelectionOptions (JsonTypeInfo typeInfo)
    {
        var options = CreateOptions(typeInfo, "kind");
        options.DerivedTypes.Add(new JsonDerivedType(typeof(NoGameViewRecordingSelection), "none"));
        options.DerivedTypes.Add(new JsonDerivedType(typeof(SelectedGameViewRecordingSelection), "selected"));
        return options;
    }

    private static JsonPolymorphismOptions CreateStopResultOptions (JsonTypeInfo typeInfo)
    {
        var options = CreateOptions(typeInfo, "payloadKind");
        options.DerivedTypes.Add(new JsonDerivedType(typeof(GameViewRecordingRecoveryPayload), "recovery"));
        options.DerivedTypes.Add(new JsonDerivedType(typeof(GameViewRecordingTerminalPayload), "terminal"));
        return options;
    }

    private static JsonPolymorphismOptions CreateOptions (JsonTypeInfo typeInfo, string propertyName)
    {
        return new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = typeInfo.Options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName,
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
    }
}
