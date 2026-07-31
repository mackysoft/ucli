using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary>
/// Carries either a reverified host-exit Terminal Record or the reconnectable reference retained
/// when its publication failed.
/// </summary>
internal abstract record LifecycleExecutionHostExitTerminalizationResult
{
    private LifecycleExecutionHostExitTerminalizationResult ()
    {
    }

    /// <summary> Carries a reverified Terminal Record and its terminal reference. </summary>
    internal sealed record Published : LifecycleExecutionHostExitTerminalizationResult
    {
        /// <summary> Initializes one published terminal result. </summary>
        public Published (
            TerminalExecutionRef executionReference,
            LifecycleExecutionTerminalRecord terminalRecord)
        {
            ExecutionReference = executionReference
                ?? throw new ArgumentNullException(nameof(executionReference));
            TerminalRecord = terminalRecord
                ?? throw new ArgumentNullException(nameof(terminalRecord));
        }

        /// <summary> Gets the reverified terminal execution reference. </summary>
        public TerminalExecutionRef ExecutionReference { get; }

        /// <summary> Gets the reverified immutable Terminal Record. </summary>
        public LifecycleExecutionTerminalRecord TerminalRecord { get; }
    }

    /// <summary> Carries a publication failure and the reference retained for reconnection. </summary>
    internal sealed record PublicationFailed :
        LifecycleExecutionHostExitTerminalizationResult
    {
        /// <summary> Initializes one failed publication result. </summary>
        public PublicationFailed (
            ExecutionRef executionReference,
            ExecutionApplicationState applicationState,
            ApplicationFailure failure,
            LifecycleExecutionTerminalRecord? fixedTerminalRecord = null)
        {
            if (executionReference is not IReconnectableExecutionRef)
            {
                throw new ArgumentException(
                    "Failed host-exit terminal publication must retain a reconnectable reference.",
                    nameof(executionReference));
            }

            ExecutionReference = executionReference;
            if (!TextVocabulary.IsDefined(applicationState))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(applicationState),
                    applicationState,
                    "A fixed-host-exit publication failure must retain a defined application state.");
            }

            ApplicationState = applicationState;
            Failure = failure
                ?? throw new ArgumentNullException(nameof(failure));
            if (fixedTerminalRecord is not null)
            {
                var referenceKind = LifecycleExecutionContractGuard.RequireReference(
                    executionReference,
                    nameof(executionReference),
                    allowTerminal: false);
                if (fixedTerminalRecord.ExecutionKind != referenceKind
                    || fixedTerminalRecord.ExecutionId != executionReference.Id
                    || fixedTerminalRecord.DefinitionDigest
                        != executionReference.DefinitionDigest)
                {
                    throw new ArgumentException(
                        "A fixed Terminal Record must retain the reconnectable execution identity.",
                        nameof(fixedTerminalRecord));
                }
                if (fixedTerminalRecord.ApplicationState != applicationState)
                {
                    throw new ArgumentException(
                        "A fixed Terminal Record must retain the publication failure application state.",
                        nameof(fixedTerminalRecord));
                }
            }

            FixedTerminalRecord = fixedTerminalRecord;
        }

        /// <summary> Gets the reconnectable execution reference safe to publish. </summary>
        public ExecutionRef ExecutionReference { get; }

        /// <summary> Gets the action application state fixed before publication failed. </summary>
        public ExecutionApplicationState ApplicationState { get; }

        /// <summary> Gets the Terminal Record publication failure. </summary>
        public ApplicationFailure Failure { get; }

        /// <summary>
        /// Gets the immutable Terminal Record whose exact content was fixed before publication
        /// failed, or <see langword="null" /> when no record was durably established.
        /// </summary>
        public LifecycleExecutionTerminalRecord? FixedTerminalRecord { get; }
    }
}
