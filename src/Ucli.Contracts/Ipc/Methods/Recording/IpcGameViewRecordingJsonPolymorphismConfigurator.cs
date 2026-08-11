using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class IpcGameViewRecordingJsonPolymorphismConfigurator
{
    public static bool TryConfigure (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(IpcGameViewRecordingSnapshot))
        {
            typeInfo.PolymorphismOptions = CreateSnapshotOptions(
                typeInfo,
                includeActive: true,
                includeRecovery: true,
                includeTerminal: true);
            return true;
        }
        if (typeInfo.Type == typeof(IpcGameViewRecordingStopSnapshot))
        {
            typeInfo.PolymorphismOptions = CreateSnapshotOptions(
                typeInfo,
                includeActive: false,
                includeRecovery: true,
                includeTerminal: true);
            return true;
        }
        if (typeInfo.Type == typeof(IpcGameViewRecordingTerminalSnapshot))
        {
            typeInfo.PolymorphismOptions = CreateSnapshotOptions(
                typeInfo,
                includeActive: false,
                includeRecovery: false,
                includeTerminal: true);
            return true;
        }
        if (typeInfo.Type == typeof(IpcGameViewRecordingSelection))
        {
            typeInfo.PolymorphismOptions = CreateSelectionOptions(typeInfo);
            return true;
        }

        return false;
    }

    private static JsonPolymorphismOptions CreateSnapshotOptions (
        JsonTypeInfo typeInfo,
        bool includeActive,
        bool includeRecovery,
        bool includeTerminal)
    {
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = GetPropertyName(typeInfo, "snapshotKind"),
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        if (includeActive)
        {
            options.DerivedTypes.Add(new JsonDerivedType(
                typeof(IpcGameViewRecordingActiveSnapshot),
                "active"));
        }
        if (includeRecovery)
        {
            options.DerivedTypes.Add(new JsonDerivedType(
                typeof(IpcGameViewRecordingRecoverySnapshot),
                "recovery"));
        }
        if (includeTerminal)
        {
            options.DerivedTypes.Add(new JsonDerivedType(
                typeof(IpcGameViewRecordingCompletedSnapshot),
                "completed"));
            options.DerivedTypes.Add(new JsonDerivedType(
                typeof(IpcGameViewRecordingFailedSnapshot),
                "failed"));
            options.DerivedTypes.Add(new JsonDerivedType(
                typeof(IpcGameViewRecordingIndeterminateSnapshot),
                "indeterminate"));
        }

        return options;
    }

    private static JsonPolymorphismOptions CreateSelectionOptions (JsonTypeInfo typeInfo)
    {
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = GetPropertyName(typeInfo, "kind"),
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        options.DerivedTypes.Add(new JsonDerivedType(typeof(IpcNoGameViewRecordingSelection), "none"));
        options.DerivedTypes.Add(new JsonDerivedType(typeof(IpcSelectedGameViewRecordingSelection), "selected"));
        return options;
    }

    private static string GetPropertyName (JsonTypeInfo typeInfo, string propertyName) =>
        typeInfo.Options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;
}
