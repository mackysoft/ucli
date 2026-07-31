using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.JsonSchema.Generation.Metadata;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Json.Generation;

/// <summary>
/// Projects numeric invariants owned by Lifecycle Execution runtime contracts.
/// </summary>
internal static class LifecycleExecutionNumericMetadata
{
    internal static void Register (JsonContractMetadataRegistry registry)
    {
        if (registry == null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        registry
            .RegisterProvider<long>(new NonNegativeInt64MetadataProvider())
            .RegisterProvider<long?>(
                new NullableNonNegativeInt64MetadataProvider())
            .RegisterProvider<int>(new Int32MinimumMetadataProvider())
            .RegisterProvider<ulong>(new UInt64MinimumMetadataProvider());
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

    private sealed class NonNegativeInt64MetadataProvider
        : IJsonContractMetadataProvider<long>
    {
        public string StableId =>
            "ucli.lifecycle-execution.numeric.non-negative-int64";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<long> context,
            JsonContractMetadataBuilder<long> builder)
        {
            if (context.DeclaringTypeInfo.Type
                    == typeof(UnityEditorGenerationSnapshot)
                || IsProperty(
                    context,
                    typeof(RefreshLifecycleResult.RefreshEvidence),
                    nameof(RefreshLifecycleResult.RefreshEvidence.DomainReloadGenerationBefore),
                    nameof(RefreshLifecycleResult.RefreshEvidence.DomainReloadGenerationAfter)))
            {
                builder.SetMinimum(JsonContractNumber.FromInt64(0));
            }
        }
    }

    private sealed class NullableNonNegativeInt64MetadataProvider
        : IJsonContractMetadataProvider<long?>
    {
        public string StableId =>
            "ucli.lifecycle-execution.numeric.nullable-non-negative-int64";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<long?> context,
            JsonContractMetadataBuilder<long?> builder)
        {
            if (HasSerializedName(
                    context,
                    nameof(CompileLifecycleResult.ScriptCompilationEvidence.CompileGenerationBefore))
                || HasSerializedName(
                    context,
                    nameof(CompileLifecycleResult.ScriptCompilationEvidence.CompileGenerationAfter))
                || HasSerializedName(
                    context,
                    nameof(CompileLifecycleResult.DomainReloadEvidence.GenerationBefore))
                || HasSerializedName(
                    context,
                    nameof(CompileLifecycleResult.DomainReloadEvidence.GenerationAfter)))
            {
                builder.SetMinimum(JsonContractNumber.FromInt64(0));
            }
        }
    }

    private sealed class Int32MinimumMetadataProvider
        : IJsonContractMetadataProvider<int>
    {
        public string StableId =>
            "ucli.lifecycle-execution.numeric.int32-minimum";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<int> context,
            JsonContractMetadataBuilder<int> builder)
        {
            if (IsProperty(
                    context,
                    typeof(ProcessIdentity),
                    nameof(ProcessIdentity.ProcessId)))
            {
                builder.SetMinimum(JsonContractNumber.FromInt64(1));
                return;
            }

            if (HasSerializedName(
                    context,
                    nameof(CompileLifecycleResult.DiagnosticsEvidence.ErrorCount))
                || HasSerializedName(
                    context,
                    nameof(CompileLifecycleResult.DiagnosticsEvidence.WarningCount)))
            {
                builder.SetMinimum(JsonContractNumber.FromInt64(0));
            }
        }
    }

    private sealed class UInt64MinimumMetadataProvider
        : IJsonContractMetadataProvider<ulong>
    {
        public string StableId =>
            "ucli.lifecycle-execution.numeric.uint64-minimum";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<ulong> context,
            JsonContractMetadataBuilder<ulong> builder)
        {
            if (IsProperty(
                    context,
                    typeof(ProcessIdentity),
                    nameof(ProcessIdentity.Generation)))
            {
                builder.SetMinimum(JsonContractNumber.FromUInt64(1));
            }
        }
    }
}
