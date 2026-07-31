using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.JsonSchema.Generation.Metadata;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Json.Metadata;

namespace MackySoft.Ucli.Contracts.Json.Generation;

/// <summary>
/// Projects action identity and verdict ownership from concrete Lifecycle Execution terminal records.
/// </summary>
internal static class LifecycleExecutionTerminalRecordMetadata
{
    private const string UtcTimestampPattern =
        "^[0-9]{4}-(0[1-9]|1[0-2])-([0-2][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,16})?(Z|\\+00:00)$(?![\\s\\S])";

    internal static void Register (JsonContractMetadataRegistry registry)
    {
        if (registry == null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        LifecycleExecutionNumericMetadata.Register(registry);
        registry
            .RegisterProvider<Sha256Digest>(
                new DefinitionDigestMetadataProvider())
            .RegisterProvider<Verdict?>(
                new VerdictMetadataProvider())
            .RegisterProvider<PlayLifecycleTransitionCommand>(
                new PlayTransitionMetadataProvider())
            .RegisterProvider<PlayLifecycleTransitionOutcome>(
                new PlayOutcomeMetadataProvider())
            .RegisterProvider<ProjectFingerprint>(
                new ProjectFingerprintMetadataProvider())
            .RegisterProvider<DateTimeOffset>(
                new UtcTimestampMetadataProvider())
            .RegisterProvider<DateTimeOffset?>(
                new NullableUtcTimestampMetadataProvider());
    }

    private static bool TryGetExecutionKind (
        Type recordType,
        out LifecycleExecutionKind executionKind)
    {
        if (recordType == typeof(RefreshLifecycleExecutionTerminalRecord))
        {
            executionKind = LifecycleExecutionKind.Refresh;
            return true;
        }
        if (recordType == typeof(CompileLifecycleExecutionTerminalRecord))
        {
            executionKind = LifecycleExecutionKind.Compile;
            return true;
        }
        if (recordType == typeof(PlayEnterLifecycleExecutionTerminalRecord))
        {
            executionKind = LifecycleExecutionKind.PlayEnter;
            return true;
        }
        if (recordType == typeof(PlayExitLifecycleExecutionTerminalRecord))
        {
            executionKind = LifecycleExecutionKind.PlayExit;
            return true;
        }

        executionKind = default;
        return false;
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

    private static bool TryGetPlayTransition (
        Type resultType,
        out PlayLifecycleTransitionCommand transition)
    {
        if (resultType == typeof(PlayEnterLifecycleTransitionResult))
        {
            transition = PlayLifecycleTransitionCommand.Enter;
            return true;
        }
        if (resultType == typeof(PlayExitLifecycleTransitionResult))
        {
            transition = PlayLifecycleTransitionCommand.Exit;
            return true;
        }

        transition = default;
        return false;
    }

    private sealed class DefinitionDigestMetadataProvider
        : IJsonContractMetadataProvider<Sha256Digest>
    {
        public string StableId =>
            "ucli.lifecycle-execution-terminal-record.definition-digest";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<Sha256Digest> context,
            JsonContractMetadataBuilder<Sha256Digest> builder)
        {
            if (TryGetExecutionKind(
                    context.DeclaringTypeInfo.Type,
                    out var executionKind)
                && HasSerializedName(
                    context,
                    nameof(LifecycleExecutionTerminalRecord.DefinitionDigest)))
            {
                builder.SetConst(LifecycleExecutionDefinitionDigest.Calculate(
                    new LifecycleExecutionDefinition(executionKind)));
            }
        }
    }

    private sealed class VerdictMetadataProvider
        : IJsonContractMetadataProvider<Verdict?>
    {
        public string StableId =>
            "ucli.lifecycle-execution-terminal-record.verdict";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<Verdict?> context,
            JsonContractMetadataBuilder<Verdict?> builder)
        {
            if (TryGetExecutionKind(
                    context.DeclaringTypeInfo.Type,
                    out var executionKind)
                && executionKind != LifecycleExecutionKind.Compile
                && HasSerializedName(
                    context,
                    nameof(LifecycleExecutionTerminalRecord.Verdict)))
            {
                builder.SetConst(null);
            }
        }
    }

    private sealed class PlayTransitionMetadataProvider
        : IJsonContractMetadataProvider<PlayLifecycleTransitionCommand>
    {
        public string StableId =>
            "ucli.lifecycle-execution-terminal-record.play-transition";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<PlayLifecycleTransitionCommand> context,
            JsonContractMetadataBuilder<PlayLifecycleTransitionCommand> builder)
        {
            if (TryGetPlayTransition(
                    context.DeclaringTypeInfo.Type,
                    out var transition)
                && HasSerializedName(
                    context,
                    nameof(PlayLifecycleTransitionResult.Transition)))
            {
                builder.SetConst(transition);
            }
        }
    }

    private sealed class PlayOutcomeMetadataProvider
        : IJsonContractMetadataProvider<PlayLifecycleTransitionOutcome>
    {
        public string StableId =>
            "ucli.lifecycle-execution-terminal-record.play-outcome";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<PlayLifecycleTransitionOutcome> context,
            JsonContractMetadataBuilder<PlayLifecycleTransitionOutcome> builder)
        {
            if (!TryGetPlayTransition(
                    context.DeclaringTypeInfo.Type,
                    out var transition)
                || !HasSerializedName(
                    context,
                    nameof(PlayLifecycleTransitionResult.Result)))
            {
                return;
            }

            var outcomes = Enum.GetValues(typeof(PlayLifecycleTransitionOutcome))
                .Cast<PlayLifecycleTransitionOutcome>()
                .Where(outcome =>
                    PlayLifecycleTransitionResult.IsCompatible(
                        transition,
                        outcome));
            builder.SetPattern(TextVocabularySubsetPattern.Create(outcomes));
        }
    }

    private sealed class ProjectFingerprintMetadataProvider
        : IJsonContractMetadataProvider<ProjectFingerprint>
    {
        public string StableId =>
            "ucli.lifecycle-execution-terminal-record.project-fingerprint";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<ProjectFingerprint> context,
            JsonContractMetadataBuilder<ProjectFingerprint> builder)
        {
            builder.SetMinimumLength(ProjectFingerprint.CanonicalTextLength);
            builder.SetMaximumLength(ProjectFingerprint.CanonicalTextLength);
            builder.SetPattern(ProjectFingerprint.CanonicalTextPattern);
        }
    }

    private sealed class UtcTimestampMetadataProvider
        : IJsonContractMetadataProvider<DateTimeOffset>
    {
        public string StableId =>
            "ucli.lifecycle-execution-terminal-record.utc-timestamp";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<DateTimeOffset> context,
            JsonContractMetadataBuilder<DateTimeOffset> builder)
        {
            if ((TryGetExecutionKind(
                    context.DeclaringTypeInfo.Type,
                    out _)
                && (HasSerializedName(
                        context,
                        nameof(LifecycleExecutionTerminalRecord.DeadlineUtc))
                    || HasSerializedName(
                        context,
                        nameof(LifecycleExecutionTerminalRecord.StartedAtUtc))
                    || HasSerializedName(
                        context,
                        nameof(LifecycleExecutionTerminalRecord.CompletedAtUtc))))
                || IsProperty(
                    context,
                    typeof(RefreshLifecycleResult.RefreshEvidence),
                    nameof(RefreshLifecycleResult.RefreshEvidence.StartedAtUtc),
                    nameof(RefreshLifecycleResult.RefreshEvidence.CompletedAtUtc))
                || IsProperty(
                    context,
                    typeof(CompileLifecycleResult.RefreshEvidence),
                    nameof(CompileLifecycleResult.RefreshEvidence.StartedAtUtc))
                || IsProperty(
                    context,
                    typeof(UnityEditorObservation),
                    nameof(UnityEditorObservation.ObservedAtUtc))
                || IsProperty(
                    context,
                    typeof(ExecutionReadPostconditionRequirement),
                    nameof(ExecutionReadPostconditionRequirement.MinSafeGeneratedAtUtc)))
            {
                builder.SetPattern(UtcTimestampPattern);
            }
        }
    }

    private sealed class NullableUtcTimestampMetadataProvider
        : IJsonContractMetadataProvider<DateTimeOffset?>
    {
        public string StableId =>
            "ucli.lifecycle-execution-terminal-record.nullable-utc-timestamp";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<DateTimeOffset?> context,
            JsonContractMetadataBuilder<DateTimeOffset?> builder)
        {
            if (IsProperty(
                    context,
                    typeof(CompileLifecycleResult.RefreshEvidence),
                    nameof(CompileLifecycleResult.RefreshEvidence.CompletedAtUtc))
                || IsProperty(
                    context,
                    typeof(CompileLifecycleResult.LifecycleEvidence),
                    nameof(CompileLifecycleResult.LifecycleEvidence.ObservedAtUtc)))
            {
                builder.SetPattern(UtcTimestampPattern);
            }
        }
    }
}
