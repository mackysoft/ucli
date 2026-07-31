using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.JsonSchema.Generation.Metadata;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json.Metadata;

namespace MackySoft.Ucli.Contracts.Json.Generation;

/// <summary>
/// Projects one action-owned Lifecycle Execution CLI result from its runtime contract.
/// </summary>
internal static class LifecycleExecutionCliOutputMetadata
{
    internal static void Register (
        JsonContractMetadataRegistry registry,
        LifecycleExecutionKind executionKind,
        CommandResultStatus status)
    {
        if (registry == null)
        {
            throw new ArgumentNullException(nameof(registry));
        }
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

        var definition = new LifecycleExecutionDefinition(executionKind);
        LifecycleExecutionNumericMetadata.Register(registry);
        registry
            .RegisterProvider<ExecutionKind>(
                new ExecutionKindMetadataProvider(definition.ExecutionKind))
            .RegisterProvider<Sha256Digest>(
                new DefinitionDigestMetadataProvider(
                    LifecycleExecutionDefinitionDigest.Calculate(definition)))
            .RegisterProvider<ExecutionState>(
                new ExecutionStateMetadataProvider(executionKind, status))
            .RegisterProvider<PlayLifecycleTransitionCommand>(
                new PlayTransitionMetadataProvider(executionKind))
            .RegisterProvider<PlayLifecycleTransitionOutcome>(
                new PlayOutcomeMetadataProvider(executionKind, status))
            .RegisterProvider<ExecutionApplicationState>(
                new ApplicationStateMetadataProvider(
                    executionKind,
                    status))
            .RegisterProvider<ArtifactKind>(
                new TerminalRecordArtifactKindMetadataProvider())
            .RegisterProvider<ArtifactMediaType>(
                new TerminalRecordArtifactMediaTypeMetadataProvider());
    }

    internal static void RegisterDefaultExecutionState (
        JsonContractMetadataRegistry registry)
    {
        if (registry == null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        registry.RegisterProvider<ExecutionState>(
            new ExecutionStateMetadataProvider());
    }

    private static bool IsExecutionReferenceProperty<TValue> (
        JsonContractMetadataContext<TValue> context,
        string propertyName)
    {
        return context.PropertyInfo != null
            && IsExecutionReferenceType(context.DeclaringTypeInfo.Type)
            && HasSerializedName(context, propertyName);
    }

    private static bool IsExecutionReferenceType (Type type)
    {
        return typeof(ExecutionRef).IsAssignableFrom(type);
    }

    private static bool IsArtifactReferenceProperty<TValue> (
        JsonContractMetadataContext<TValue> context,
        string propertyName)
    {
        return context.PropertyInfo != null
            && typeof(ArtifactRef).IsAssignableFrom(
                context.DeclaringTypeInfo.Type)
            && HasSerializedName(context, propertyName);
    }

    private static bool HasSerializedName<TValue> (
        JsonContractMetadataContext<TValue> context,
        string propertyName)
    {
        var namingPolicy = context.DeclaringTypeInfo.Options.PropertyNamingPolicy;
        var serializedName = namingPolicy?.ConvertName(propertyName) ?? propertyName;
        return string.Equals(
            context.PropertyInfo!.Name,
            serializedName,
            StringComparison.Ordinal);
    }

    private static ExecutionLifecycle GetLifecycle (Type referenceType)
    {
        if (referenceType == typeof(ActiveExecutionRef))
        {
            return ExecutionLifecycle.Active;
        }
        if (referenceType == typeof(RecoveryExecutionRef))
        {
            return ExecutionLifecycle.Recovery;
        }
        if (referenceType == typeof(TerminalExecutionRef))
        {
            return ExecutionLifecycle.Terminal;
        }
        throw new ArgumentOutOfRangeException(
            nameof(referenceType),
            referenceType,
            "Lifecycle Execution metadata supports only concrete execution-reference branches.");
    }

    private static IEnumerable<LifecycleExecutionState> GetAllowedStates (
        LifecycleExecutionKind executionKind,
        CommandResultStatus status,
        ExecutionLifecycle lifecycle)
    {
        if (lifecycle == ExecutionLifecycle.Terminal)
        {
            yield return status == CommandResultStatus.Ok
                ? LifecycleExecutionState.Completed
                : LifecycleExecutionState.Failed;
            yield break;
        }

        foreach (var state in Enum.GetValues(typeof(LifecycleExecutionState))
            .Cast<LifecycleExecutionState>())
        {
            if (LifecycleExecutionContractGuard.IsStateAllowed(
                executionKind,
                lifecycle,
                state))
            {
                yield return state;
            }
        }
    }

    private static IEnumerable<PlayLifecycleTransitionOutcome> GetAllowedPlayOutcomes (
        PlayLifecycleTransitionCommand transition,
        CommandResultStatus status,
        UcliPlayTransitionOutcomeSubset? subset)
    {
        foreach (var outcome in Enum.GetValues(typeof(PlayLifecycleTransitionOutcome))
            .Cast<PlayLifecycleTransitionOutcome>())
        {
            if (!PlayLifecycleTransitionResult.IsCompatible(
                    transition,
                    outcome))
            {
                continue;
            }

            var isSuccessful =
                PlayLifecycleTransitionResult.IsSuccessfulOutcome(outcome);
            if (subset == UcliPlayTransitionOutcomeSubset.Success
                && !isSuccessful)
            {
                continue;
            }
            if (subset == UcliPlayTransitionOutcomeSubset.Failure
                && isSuccessful)
            {
                continue;
            }
            if (!subset.HasValue
                && status == CommandResultStatus.Ok
                && !isSuccessful)
            {
                continue;
            }

            yield return outcome;
        }
    }

    private static IEnumerable<ExecutionApplicationState>
        GetAllowedApplicationStates (
            LifecycleExecutionKind executionKind)
    {
        foreach (var applicationState
            in Enum.GetValues(typeof(ExecutionApplicationState))
                .Cast<ExecutionApplicationState>())
        {
            if (executionKind != LifecycleExecutionKind.Compile
                && applicationState
                    == ExecutionApplicationState.PartiallyApplied)
            {
                continue;
            }

            yield return applicationState;
        }
    }

    private sealed class ExecutionKindMetadataProvider
        : IJsonContractMetadataProvider<ExecutionKind>
    {
        private readonly ExecutionKind executionKind;

        public ExecutionKindMetadataProvider (ExecutionKind executionKind)
        {
            this.executionKind = executionKind;
        }

        public string StableId => "ucli.lifecycle-execution-cli.execution-kind";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<ExecutionKind> context,
            JsonContractMetadataBuilder<ExecutionKind> builder)
        {
            if (IsExecutionReferenceProperty(context, nameof(ExecutionRef.Kind)))
            {
                builder.SetConst(executionKind);
            }
        }
    }

    private sealed class DefinitionDigestMetadataProvider
        : IJsonContractMetadataProvider<Sha256Digest>
    {
        private readonly Sha256Digest definitionDigest;

        public DefinitionDigestMetadataProvider (Sha256Digest definitionDigest)
        {
            this.definitionDigest = definitionDigest;
        }

        public string StableId =>
            "ucli.lifecycle-execution-cli.definition-digest";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<Sha256Digest> context,
            JsonContractMetadataBuilder<Sha256Digest> builder)
        {
            if (IsExecutionReferenceProperty(
                context,
                nameof(ExecutionRef.DefinitionDigest)))
            {
                builder.SetConst(definitionDigest);
            }
        }
    }

    private sealed class ExecutionStateMetadataProvider
        : IJsonContractMetadataProvider<ExecutionState>
    {
        private readonly LifecycleExecutionKind? executionKind;
        private readonly CommandResultStatus? status;

        public ExecutionStateMetadataProvider ()
        {
        }

        public ExecutionStateMetadataProvider (
            LifecycleExecutionKind executionKind,
            CommandResultStatus status)
        {
            this.executionKind = executionKind;
            this.status = status;
        }

        public string StableId => "ucli.execution-state";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<ExecutionState> context,
            JsonContractMetadataBuilder<ExecutionState> builder)
        {
            if (!executionKind.HasValue)
            {
                if (context.PropertyInfo == null)
                {
                    builder.SetPattern(
                        ReferenceTextContract.DotSeparatedLowerCamelPattern);
                }

                return;
            }

            if (IsExecutionReferenceProperty(
                context,
                nameof(ExecutionRef.State)))
            {
                var allowedStates = GetAllowedStates(
                        executionKind.Value,
                        status.GetValueOrDefault(),
                        GetLifecycle(context.DeclaringTypeInfo.Type))
                    .ToArray();
                if (allowedStates.Length == 1)
                {
                    builder.SetConst(new ExecutionState(
                        TextVocabulary.GetText(allowedStates[0])));
                }
                else
                {
                    builder.SetPattern(
                        TextVocabularySubsetPattern.Create(allowedStates));
                }

                return;
            }

            if (context.PropertyInfo != null)
            {
                builder.SetPattern(
                    ReferenceTextContract.DotSeparatedLowerCamelPattern);
            }
        }
    }

    private sealed class PlayTransitionMetadataProvider
        : IJsonContractMetadataProvider<PlayLifecycleTransitionCommand>
    {
        private readonly PlayLifecycleTransitionCommand? transition;

        public PlayTransitionMetadataProvider (
            LifecycleExecutionKind executionKind)
        {
            transition = executionKind switch
            {
                LifecycleExecutionKind.PlayEnter =>
                    PlayLifecycleTransitionCommand.Enter,
                LifecycleExecutionKind.PlayExit =>
                    PlayLifecycleTransitionCommand.Exit,
                _ => null,
            };
        }

        public string StableId => "ucli.lifecycle-execution-cli.play-transition";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<PlayLifecycleTransitionCommand> context,
            JsonContractMetadataBuilder<PlayLifecycleTransitionCommand> builder)
        {
            if (transition.HasValue
                && context.PropertyInfo != null
                && HasSerializedName(
                    context,
                    nameof(PlayLifecycleTransitionResult.Transition)))
            {
                builder.SetConst(transition.Value);
            }
        }
    }

    private sealed class PlayOutcomeMetadataProvider
        : IJsonContractMetadataProvider<PlayLifecycleTransitionOutcome>
    {
        private readonly PlayLifecycleTransitionCommand? transition;
        private readonly CommandResultStatus status;

        public PlayOutcomeMetadataProvider (
            LifecycleExecutionKind executionKind,
            CommandResultStatus status)
        {
            transition = executionKind switch
            {
                LifecycleExecutionKind.PlayEnter =>
                    PlayLifecycleTransitionCommand.Enter,
                LifecycleExecutionKind.PlayExit =>
                    PlayLifecycleTransitionCommand.Exit,
                _ => null,
            };
            this.status = status;
        }

        public string StableId => "ucli.lifecycle-execution-cli.play-outcome";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<PlayLifecycleTransitionOutcome> context,
            JsonContractMetadataBuilder<PlayLifecycleTransitionOutcome> builder)
        {
            if (transition.HasValue
                && context.PropertyInfo != null
                && HasSerializedName(
                    context,
                    nameof(PlayLifecycleTransitionResult.Result)))
            {
                var attribute = context.PropertyInfo.AttributeProvider?
                    .GetCustomAttributes(
                        typeof(UcliPlayTransitionOutcomeSubsetAttribute),
                        inherit: true)
                    .Cast<UcliPlayTransitionOutcomeSubsetAttribute>()
                    .SingleOrDefault();
                builder.SetPattern(TextVocabularySubsetPattern.Create(
                    GetAllowedPlayOutcomes(
                        transition.Value,
                        status,
                        attribute?.Subset)));
            }
        }
    }

    private sealed class ApplicationStateMetadataProvider
        : IJsonContractMetadataProvider<ExecutionApplicationState>
    {
        private readonly LifecycleExecutionKind executionKind;
        private readonly CommandResultStatus status;

        public ApplicationStateMetadataProvider (
            LifecycleExecutionKind executionKind,
            CommandResultStatus status)
        {
            this.executionKind = executionKind;
            this.status = status;
        }

        public string StableId =>
            "ucli.lifecycle-execution-cli.application-state";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<ExecutionApplicationState> context,
            JsonContractMetadataBuilder<ExecutionApplicationState> builder)
        {
            if (status == CommandResultStatus.Error
                && context.PropertyInfo != null
                && context.PropertyInfo.AttributeProvider?.IsDefined(
                    typeof(UcliOperationApplicationStateAttribute),
                    inherit: true) != true
                && HasSerializedName(
                    context,
                    nameof(IpcRefreshErrorResponse.ApplicationState)))
            {
                builder.SetPattern(TextVocabularySubsetPattern.Create(
                    GetAllowedApplicationStates(executionKind)));
            }
        }
    }

    private sealed class TerminalRecordArtifactKindMetadataProvider
        : IJsonContractMetadataProvider<ArtifactKind>
    {
        public string StableId =>
            "ucli.lifecycle-execution-cli.terminal-record-kind";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<ArtifactKind> context,
            JsonContractMetadataBuilder<ArtifactKind> builder)
        {
            if (IsArtifactReferenceProperty(
                context,
                nameof(ArtifactRef.Kind)))
            {
                builder.SetConst(
                    LifecycleExecutionArtifactContract.TerminalRecordKind);
            }
        }
    }

    private sealed class TerminalRecordArtifactMediaTypeMetadataProvider
        : IJsonContractMetadataProvider<ArtifactMediaType>
    {
        public string StableId =>
            "ucli.lifecycle-execution-cli.terminal-record-media-type";

        public string ContractVersion => "1";

        public void ProvideMetadata (
            JsonContractMetadataContext<ArtifactMediaType> context,
            JsonContractMetadataBuilder<ArtifactMediaType> builder)
        {
            if (IsArtifactReferenceProperty(
                context,
                nameof(ArtifactRef.MediaType)))
            {
                builder.SetConst(
                    LifecycleExecutionArtifactContract.TerminalRecordMediaType);
            }
        }
    }
}
