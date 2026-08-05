using System.Text.RegularExpressions;
using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.JsonSchema.Generation.Metadata;
using MackySoft.Ucli.Contracts.Json.Metadata;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Contracts.Json.Generation;

/// <summary>Projects constructor-enforced scalar invariants onto GameView recording output contracts.</summary>
internal static class GameViewRecordingOutputMetadata
{
    private const string UtcTimestampPattern =
        "^[0-9]{4}-(0[1-9]|1[0-2])-([0-2][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,16})?(Z|\\+00:00)$(?![\\s\\S])";

    private const string NonBlankTextPattern =
        "^[\\u0009-\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028-\\u2029\\u202F\\u205F\\u3000]*"
        + "[^\\u0009-\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028-\\u2029\\u202F\\u205F\\u3000]"
        + "(.|\\n|\\r|\\u2028|\\u2029)*$(?![\\s\\S])";

    internal static void Register (JsonContractMetadataRegistry registry)
    {
        if (registry == null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        registry
            .RegisterProvider<ExecutionKind>(new ExecutionKindMetadataProvider())
            .RegisterProvider<ExecutionState>(new ExecutionStateMetadataProvider())
            .RegisterProvider<GameViewRecordingState>(new RecordingStateMetadataProvider())
            .RegisterProvider<DateTimeOffset>(new UtcTimestampMetadataProvider())
            .RegisterProvider<DateTimeOffset?>(new NullableUtcTimestampMetadataProvider())
            .RegisterProvider<string>(new TextMetadataProvider());
    }

    private static bool HasSerializedName<TValue> (
        JsonContractMetadataContext<TValue> context,
        string propertyName)
    {
        if (context.PropertyInfo == null)
        {
            return false;
        }

        var namingPolicy = context.DeclaringTypeInfo.Options.PropertyNamingPolicy;
        var serializedName = namingPolicy?.ConvertName(propertyName) ?? propertyName;
        return string.Equals(
            context.PropertyInfo.Name,
            serializedName,
            StringComparison.Ordinal);
    }

    private static bool IsProperty<TValue> (
        JsonContractMetadataContext<TValue> context,
        Type declaringType,
        params string[] propertyNames)
    {
        return context.DeclaringTypeInfo.Type == declaringType
            && propertyNames.Any(propertyName =>
                HasSerializedName(context, propertyName));
    }

    private static bool IsProgressProperty<TValue> (
        JsonContractMetadataContext<TValue> context,
        params string[] propertyNames)
    {
        return typeof(GameViewRecordingProgress).IsAssignableFrom(
                context.DeclaringTypeInfo.Type)
            && propertyNames.Any(propertyName =>
                HasSerializedName(context, propertyName));
    }

    private static bool IsTerminalSummaryProperty<TValue> (
        JsonContractMetadataContext<TValue> context,
        params string[] propertyNames)
    {
        return context.DeclaringTypeInfo.Type
                == typeof(GameViewRecordingTerminalSummary)
            && propertyNames.Any(propertyName =>
                HasSerializedName(context, propertyName));
    }

    private static string CreateExactPattern (string value) =>
        "^" + Regex.Escape(value) + "$(?![\\s\\S])";

    private static bool IsExecutionReferenceProperty<TValue> (
        JsonContractMetadataContext<TValue> context,
        string propertyName) =>
        typeof(ExecutionRef).IsAssignableFrom(
                context.DeclaringTypeInfo.Type)
            && HasSerializedName(context, propertyName);

    private static ExecutionLifecycle GetLifecycle (Type type)
    {
        if (type == typeof(ActiveExecutionRef)
            || type == typeof(GameViewRecordingActiveProgress))
        {
            return ExecutionLifecycle.Active;
        }
        if (type == typeof(RecoveryExecutionRef)
            || type == typeof(GameViewRecordingRecoveryProgress))
        {
            return ExecutionLifecycle.Recovery;
        }
        if (type == typeof(TerminalExecutionRef)
            || type == typeof(GameViewRecordingTerminalProgress)
            || type == typeof(GameViewRecordingTerminalSummary))
        {
            return ExecutionLifecycle.Terminal;
        }

        throw new ArgumentOutOfRangeException(
            nameof(type),
            type,
            "GameView recording output metadata supports only concrete lifecycle branches.");
    }

    private static GameViewRecordingState[] GetAllowedStates (ExecutionLifecycle lifecycle) =>
        Enum.GetValues(typeof(GameViewRecordingState))
            .Cast<GameViewRecordingState>()
            .Where(state => GameViewRecordingExecutionContract.GetLifecycle(state) == lifecycle)
            .ToArray();

    private sealed class ExecutionKindMetadataProvider
        : IJsonContractMetadataProvider<ExecutionKind>
    {
        public string StableId => "ucli.game-view-recording-output.execution-kind";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<ExecutionKind> context,
            JsonContractMetadataBuilder<ExecutionKind> builder)
        {
            if (IsExecutionReferenceProperty(
                    context,
                    nameof(ExecutionRef.Kind)))
            {
                builder.SetConst(GameViewRecordingExecutionContract.Kind);
            }
        }
    }

    private sealed class ExecutionStateMetadataProvider
        : IJsonContractMetadataProvider<ExecutionState>
    {
        public string StableId => "ucli.game-view-recording-output.execution-state";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<ExecutionState> context,
            JsonContractMetadataBuilder<ExecutionState> builder)
        {
            if (IsExecutionReferenceProperty(
                    context,
                    nameof(ExecutionRef.State)))
            {
                builder.SetPattern(TextVocabularySubsetPattern.Create(
                    GetAllowedStates(GetLifecycle(context.DeclaringTypeInfo.Type))));
            }
        }
    }

    private sealed class RecordingStateMetadataProvider
        : IJsonContractMetadataProvider<GameViewRecordingState>
    {
        public string StableId => "ucli.game-view-recording-output.recording-state";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<GameViewRecordingState> context,
            JsonContractMetadataBuilder<GameViewRecordingState> builder)
        {
            if (IsProgressProperty(context, nameof(GameViewRecordingProgress.State))
                || IsTerminalSummaryProperty(
                    context,
                    nameof(GameViewRecordingTerminalSummary.State)))
            {
                builder.SetPattern(TextVocabularySubsetPattern.Create(
                    GetAllowedStates(GetLifecycle(context.DeclaringTypeInfo.Type))));
            }
        }
    }

    private sealed class UtcTimestampMetadataProvider
        : IJsonContractMetadataProvider<DateTimeOffset>
    {
        public string StableId => "ucli.game-view-recording-output.utc-timestamp";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<DateTimeOffset> context,
            JsonContractMetadataBuilder<DateTimeOffset> builder)
        {
            if (IsProgressProperty(
                    context,
                    nameof(GameViewRecordingProgress.UpdatedAtUtc))
                || IsTerminalSummaryProperty(
                    context,
                    nameof(GameViewRecordingTerminalSummary.CompletedAtUtc)))
            {
                builder.SetPattern(UtcTimestampPattern);
            }
        }
    }

    private sealed class NullableUtcTimestampMetadataProvider
        : IJsonContractMetadataProvider<DateTimeOffset?>
    {
        public string StableId =>
            "ucli.game-view-recording-output.nullable-utc-timestamp";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<DateTimeOffset?> context,
            JsonContractMetadataBuilder<DateTimeOffset?> builder)
        {
            if (IsProgressProperty(
                    context,
                    nameof(GameViewRecordingProgress.StartedAtUtc),
                    nameof(GameViewRecordingProgress.StopRequestedAtUtc))
                || IsTerminalSummaryProperty(
                    context,
                    nameof(GameViewRecordingTerminalSummary.StartedAtUtc)))
            {
                builder.SetPattern(UtcTimestampPattern);
            }
        }
    }

    private sealed class TextMetadataProvider
        : IJsonContractMetadataProvider<string>
    {
        public string StableId => "ucli.game-view-recording-output.text";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<string> context,
            JsonContractMetadataBuilder<string> builder)
        {
            if (IsProperty(
                    context,
                    typeof(GameViewRecordingPackageCapability),
                    nameof(GameViewRecordingPackageCapability.PackageId)))
            {
                builder.SetConst(GameViewRecorderCompatibilityMetadata.PackageId);
                return;
            }

            if (IsProperty(
                    context,
                    typeof(GameViewRecordingCompatibilityCapability),
                    nameof(GameViewRecordingCompatibilityCapability.RecorderPackageVersionRange)))
            {
                builder.SetConst(
                    GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange);
                return;
            }

            if (IsProperty(
                    context,
                    typeof(GameViewRecordingAdapterCapability),
                    nameof(GameViewRecordingAdapterCapability.AdapterId)))
            {
                builder.SetPattern(CreateExactPattern(
                    GameViewRecorderCompatibilityMetadata.AdapterId));
                return;
            }

            if (IsProperty(
                    context,
                    typeof(GameViewRecordingAdapterCapability),
                    nameof(GameViewRecordingAdapterCapability.AdapterVersion)))
            {
                builder.SetPattern(CreateExactPattern(
                    GameViewRecorderCompatibilityMetadata.AdapterVersion));
                return;
            }

            if (IsProperty(
                    context,
                    typeof(GameViewRecordingCaptureProfile),
                    nameof(GameViewRecordingCaptureProfile.EncodingProfile),
                    nameof(GameViewRecordingCaptureProfile.EncodingQuality))
                || IsProperty(
                    context,
                    typeof(GameViewRecordingDiagnostic),
                    nameof(GameViewRecordingDiagnostic.Message)))
            {
                builder.SetMinimumLength(1);
                builder.SetPattern(NonBlankTextPattern);
            }
        }
    }
}
