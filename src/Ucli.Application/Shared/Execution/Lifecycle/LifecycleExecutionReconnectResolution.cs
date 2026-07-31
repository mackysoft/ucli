using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary>
/// Carries the authoritative continuation of an existing Lifecycle Execution, or the typed reason
/// that reconnection was rejected.
/// </summary>
internal abstract record LifecycleExecutionReconnectResolution
{
    private LifecycleExecutionReconnectResolution ()
    {
    }

    /// <summary>
    /// Carries the immutable registration and Start Record required to resume waiting for an open
    /// execution through its action handler.
    /// </summary>
    internal sealed record Open : LifecycleExecutionReconnectResolution
    {
        /// <summary> Initializes one open continuation. </summary>
        public Open (
            LifecycleExecutionRegistration registration,
            ExecutionRef currentReference,
            LifecycleExecutionStartBinding requiredStart)
        {
            Registration = registration
                ?? throw new ArgumentNullException(nameof(registration));
            CurrentReference = currentReference
                ?? throw new ArgumentNullException(nameof(currentReference));
            RequiredStart = requiredStart
                ?? throw new ArgumentNullException(nameof(requiredStart));

            if (currentReference is not IReconnectableExecutionRef)
            {
                throw new ArgumentException(
                    "An open Lifecycle Execution continuation requires a reconnectable reference.",
                    nameof(currentReference));
            }
            if (!registration.HasSameIdentity(
                    requiredStart.LifecycleExecutionRef)
                || !registration.HasSameIdentity(currentReference)
                || requiredStart.DeadlineUtc != registration.DeadlineUtc
                || requiredStart.StartedAtUtc != registration.StartedAtUtc)
            {
                throw new ArgumentException(
                    "Open Lifecycle Execution reconnect facts must identify the same immutable registration.");
            }
        }

        /// <summary> Gets the authoritative immutable registration. </summary>
        public LifecycleExecutionRegistration Registration { get; }

        /// <summary> Gets the current reconnectable execution reference. </summary>
        public ExecutionRef CurrentReference { get; }

        /// <summary>
        /// Gets the durable Start Record binding provider reconnection to the original project and
        /// Unity host.
        /// </summary>
        public LifecycleExecutionStartBinding RequiredStart { get; }
    }

    /// <summary>
    /// Carries a reverified immutable Terminal Record and its authoritative terminal reference.
    /// </summary>
    internal sealed record Terminal : LifecycleExecutionReconnectResolution
    {
        /// <summary> Initializes one terminal continuation. </summary>
        public Terminal (
            TerminalExecutionRef executionReference,
            LifecycleExecutionTerminalRecord terminalRecord)
        {
            ExecutionReference = executionReference
                ?? throw new ArgumentNullException(nameof(executionReference));
            TerminalRecord = terminalRecord
                ?? throw new ArgumentNullException(nameof(terminalRecord));

            LifecycleExecutionContractGuard.RequireReference(
                executionReference,
                nameof(executionReference),
                terminalRecord.ExecutionKind);
            if (terminalRecord.ExecutionId != executionReference.Id
                || terminalRecord.DefinitionDigest
                    != executionReference.DefinitionDigest)
            {
                throw new ArgumentException(
                    "Lifecycle Execution Terminal Record must identify its authoritative terminal reference.",
                    nameof(terminalRecord));
            }
        }

        /// <summary> Gets the authoritative terminal execution reference. </summary>
        public TerminalExecutionRef ExecutionReference { get; }

        /// <summary> Gets the reverified immutable Terminal Record. </summary>
        public LifecycleExecutionTerminalRecord TerminalRecord { get; }
    }

    /// <summary>
    /// Carries a recoverable publishing reference when Terminal Record publication or
    /// reverification failed.
    /// </summary>
    internal sealed record PublicationFailed : LifecycleExecutionReconnectResolution
    {
        /// <summary> Initializes one recoverable terminal publication failure. </summary>
        public PublicationFailed (
            ApplicationFailure failure,
            ExecutionRef currentReference)
        {
            Failure = failure
                ?? throw new ArgumentNullException(nameof(failure));
            CurrentReference = currentReference
                ?? throw new ArgumentNullException(nameof(currentReference));

            if (failure.Code
                != LifecycleExecutionErrorCodes.TerminalPublicationFailed)
            {
                throw new ArgumentException(
                    "A terminal publication failure resolution requires the terminal publication failure code.",
                    nameof(failure));
            }
            if (currentReference is not IReconnectableExecutionRef)
            {
                throw new ArgumentException(
                    "A terminal publication failure resolution requires a reconnectable execution reference.",
                    nameof(currentReference));
            }
        }

        /// <summary> Gets the typed Terminal Record publication failure. </summary>
        public ApplicationFailure Failure { get; }

        /// <summary> Gets the durable reference that remains reconnectable. </summary>
        public ExecutionRef CurrentReference { get; }
    }

    /// <summary> Carries the typed failure that rejected reconnection before provider dispatch. </summary>
    internal sealed record Rejected : LifecycleExecutionReconnectResolution
    {
        /// <summary> Initializes one rejected resolution. </summary>
        public Rejected (ApplicationFailure failure)
        {
            Failure = failure
                ?? throw new ArgumentNullException(nameof(failure));
        }

        /// <summary> Gets the typed rejection reason. </summary>
        public ApplicationFailure Failure { get; }
    }
}
