using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;

namespace MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

/// <summary> Projects the common reference shape while leaving state transitions to the owning action. </summary>
internal static class LifecycleExecutionReferenceFactory
{
    public static ActiveExecutionRef CreateRegistered (
        LifecycleExecutionDefinition definition,
        Guid executionId,
        ExecutionStatusLocator statusLocator)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (executionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Lifecycle Execution identifier must not be empty.",
                nameof(executionId));
        }

        return new ActiveExecutionRef(
            definition.ExecutionKind,
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            CreateState(LifecycleExecutionState.Registered),
            statusLocator ?? throw new ArgumentNullException(nameof(statusLocator)));
    }

    public static ExecutionRef CreateStateProjection (
        ExecutionRef establishedReference,
        ExecutionLifecycle lifecycle,
        LifecycleExecutionState state)
    {
        if (establishedReference is null)
        {
            throw new ArgumentNullException(nameof(establishedReference));
        }

        if (establishedReference.Id == Guid.Empty)
        {
            throw new ArgumentException(
                "Lifecycle Execution identifier must not be empty.",
                nameof(establishedReference));
        }

        if (establishedReference.StatusLocator is null)
        {
            throw new ArgumentException(
                "Lifecycle Execution status locator must remain available before terminal publication.",
                nameof(establishedReference));
        }

        return lifecycle switch
        {
            ExecutionLifecycle.Active => new ActiveExecutionRef(
                establishedReference.Kind,
                establishedReference.Id,
                establishedReference.DefinitionDigest,
                CreateState(state),
                establishedReference.StatusLocator),
            ExecutionLifecycle.Recovery => new RecoveryExecutionRef(
                establishedReference.Kind,
                establishedReference.Id,
                establishedReference.DefinitionDigest,
                CreateState(state),
                establishedReference.StatusLocator),
            _ => throw new ArgumentOutOfRangeException(
                nameof(lifecycle),
                lifecycle,
                "Only active and recovery references can be projected before terminal publication."),
        };
    }

    /// <summary>
    /// Retains the last reconnectable reference when Terminal Record publication
    /// could not be reverified.
    /// </summary>
    public static ExecutionRef? CreateTerminalPublicationFailureProjection (
        StoredLifecycleExecution stored)
    {
        if (stored == null || !stored.IsTerminal)
        {
            return stored?.CurrentReference;
        }

        return LifecycleExecutionFailureReferenceProjection.CreatePublishing(
            stored.Start.LifecycleExecutionRef);
    }

    public static TerminalExecutionRef CreateTerminal (
        ExecutionRef establishedReference,
        LifecycleExecutionTerminalReason terminalReason,
        ArtifactRef terminalRecordReference)
    {
        if (establishedReference is null)
        {
            throw new ArgumentNullException(nameof(establishedReference));
        }

        return new TerminalExecutionRef(
            establishedReference.Kind,
            establishedReference.Id,
            establishedReference.DefinitionDigest,
            CreateState(
                terminalReason == LifecycleExecutionTerminalReason.Completed
                    ? LifecycleExecutionState.Completed
                    : LifecycleExecutionState.Failed),
            establishedReference.StatusLocator,
            terminalRecordReference ?? throw new ArgumentNullException(nameof(terminalRecordReference)));
    }

    private static ExecutionState CreateState (LifecycleExecutionState state)
    {
        if (!TextVocabulary.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "Lifecycle Execution state must be defined.");
        }

        return new ExecutionState(TextVocabulary.GetText(state));
    }
}
