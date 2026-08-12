using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Features.Programs.Supervision;

/// <summary> Dispatches and recovers one persisted logical Program Step execution. </summary>
internal interface IProgramStepExecutionPort
{
    /// <summary>
    /// Dispatches the persisted execution exactly once by its execution identifier.
    /// </summary>
    /// <param name="start">The Run, Step, and persisted execution identity to dispatch.</param>
    /// <param name="cancellationToken">Cancels only this caller's wait.</param>
    /// <returns>Whether dispatch started or requires recovery of the same identity.</returns>
    ValueTask<ProgramStepExecutionPortResult> StartAsync (
        ProgramStepExecutionStart start,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches to and recovers only the same persisted execution identifier;
    /// it never dispatches a new execution, invokes Start, or reissues a side effect.
    /// </summary>
    /// <param name="recovery">The fixed identity and bounded recovery wait.</param>
    /// <param name="cancellationToken">Cancels only this caller's wait.</param>
    /// <returns>The recovered terminal facts or the current transport disposition.</returns>
    ValueTask<ProgramStepExecutionRecoveryResult> RecoverAsync (
        ProgramStepExecutionRecovery recovery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests termination and subsequent recovery only for the same persisted execution identifier;
    /// it never dispatches, starts, or reissues a replacement side effect.
    /// </summary>
    /// <param name="termination">The timeout reason and bounded recovery wait.</param>
    /// <param name="cancellationToken">Cancels only this caller's wait.</param>
    /// <returns>Whether the request was delivered or recovery must attach to the same identity.</returns>
    ValueTask<ProgramStepExecutionTerminationResult> RequestTerminationAsync (
        ProgramStepExecutionTermination termination,
        CancellationToken cancellationToken = default);
}

/// <summary> Supplies the one persisted execution identity for its only dispatch. </summary>
internal sealed record ProgramStepExecutionStart (ProgramRunRecord Run, int StepIndex, ProgramStepExecutionReference Execution);

/// <summary> Supplies the same execution identity and its fixed bounded recovery wait. </summary>
/// <param name="Run">The persisted Program Run.</param>
/// <param name="StepIndex">The persisted Step index.</param>
/// <param name="Execution">The existing execution identifier to attach to.</param>
/// <param name="Deadline">The fixed finite recovery upper bound; it does not extend the execution deadline.</param>
/// <param name="RemainingTimeout">The remaining duration within <paramref name="Deadline" /> for this wait.</param>
internal sealed record ProgramStepExecutionRecovery (
    ProgramRunRecord Run,
    int StepIndex,
    ProgramStepExecutionReference Execution,
    ExecutionDeadline Deadline,
    TimeSpan RemainingTimeout);

/// <summary> Supplies the same execution identity, timeout reason, and bounded termination recovery wait. </summary>
/// <param name="Run">The persisted Program Run.</param>
/// <param name="StepIndex">The persisted Step index.</param>
/// <param name="Execution">The existing execution identifier to terminate and recover.</param>
/// <param name="ReasonCode">The stable timeout reason selected by the Supervisor.</param>
/// <param name="Deadline">The fixed finite recovery upper bound after timeout; it does not extend execution.</param>
/// <param name="RemainingTimeout">The remaining duration within <paramref name="Deadline" /> for this wait.</param>
internal sealed record ProgramStepExecutionTermination (
    ProgramRunRecord Run,
    int StepIndex,
    ProgramStepExecutionReference Execution,
    string ReasonCode,
    ExecutionDeadline Deadline,
    TimeSpan RemainingTimeout);

/// <summary> Reports whether starting one persisted execution left its response recoverable. </summary>
internal enum ProgramStepExecutionPortResult
{
    Started = 1,
    CommunicationLost,
}

/// <summary> Reports whether the already-started logical execution was recovered. </summary>
internal sealed record ProgramStepExecutionRecoveryResult (
    ProgramStepExecutionRecoveryDisposition Disposition,
    ProgramStepExecutionRecoveredTerminal? Terminal = null)
{
    public static ProgramStepExecutionRecoveryResult Recovered { get; } = new(ProgramStepExecutionRecoveryDisposition.Recovered);
    public static ProgramStepExecutionRecoveryResult CommunicationLost { get; } = new(ProgramStepExecutionRecoveryDisposition.CommunicationLost);

    public static ProgramStepExecutionRecoveryResult TerminallyRecovered (ProgramStepExecutionRecoveredTerminal terminal) =>
        new(ProgramStepExecutionRecoveryDisposition.Recovered, terminal ?? throw new ArgumentNullException(nameof(terminal)));
}

internal enum ProgramStepExecutionRecoveryDisposition { Recovered = 1, CommunicationLost }

/// <summary> Reports whether the same logical execution accepted a termination request. </summary>
internal enum ProgramStepExecutionTerminationResult
{
    Requested = 1,
    CommunicationLost,
}

/// <summary> Typed terminal facts recovered for the same persisted logical execution. </summary>
internal sealed record ProgramStepExecutionRecoveredTerminal (
    ProgramStepState State,
    Verdict? Verdict,
    ExecutionApplicationState ApplicationState,
    string? ErrorCode)
{
    public ProgramStepExecutionRecoveredTerminal Validate ()
    {
        if (!ProgramRunStateSemantics.IsTerminal(State) || !TextVocabulary.IsDefined(ApplicationState)
            || (Verdict.HasValue && !TextVocabulary.IsDefined(Verdict.Value)))
        {
            throw new ArgumentException("Recovered Program Step terminal facts must be terminal and typed.");
        }
        return this;
    }
}
