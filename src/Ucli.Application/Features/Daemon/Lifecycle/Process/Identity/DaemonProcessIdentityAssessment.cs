using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;

/// <summary> Represents one daemon process identity assessment result. </summary>
internal sealed class DaemonProcessIdentityAssessment
{
    private DaemonProcessIdentityAssessment (
        DaemonProcessIdentityAssessmentStatus status,
        DateTimeOffset? observedStartTimeUtc,
        ExecutionError? error)
    {
        Status = status;
        ObservedStartTimeUtc = observedStartTimeUtc;
        Error = error;
    }

    /// <summary> Creates an assessment for a process that is not running. </summary>
    public static DaemonProcessIdentityAssessment NotRunning ()
    {
        return new DaemonProcessIdentityAssessment(
            DaemonProcessIdentityAssessmentStatus.NotRunning,
            observedStartTimeUtc: null,
            error: null);
    }

    /// <summary> Creates an assessment for a live process that matches the expected identity. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="observedStartTimeUtc" /> is default or is not UTC. </exception>
    public static DaemonProcessIdentityAssessment MatchingLiveProcess (DateTimeOffset observedStartTimeUtc)
    {
        return new DaemonProcessIdentityAssessment(
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            ContractArgumentGuard.RequireUtcTimestamp(observedStartTimeUtc, nameof(observedStartTimeUtc)),
            error: null);
    }

    /// <summary> Creates an assessment for a live process that differs from the expected identity. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="observedStartTimeUtc" /> is default or is not UTC. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonProcessIdentityAssessment DifferentProcess (
        DateTimeOffset observedStartTimeUtc,
        ExecutionError error)
    {
        return new DaemonProcessIdentityAssessment(
            DaemonProcessIdentityAssessmentStatus.DifferentProcess,
            ContractArgumentGuard.RequireUtcTimestamp(observedStartTimeUtc, nameof(observedStartTimeUtc)),
            error ?? throw new ArgumentNullException(nameof(error)));
    }

    /// <summary> Creates an assessment whose process identity could not be determined. </summary>
    /// <exception cref="ArgumentException"> Thrown when a supplied <paramref name="observedStartTimeUtc" /> is default or is not UTC. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonProcessIdentityAssessment Uncertain (
        DateTimeOffset? observedStartTimeUtc,
        ExecutionError error)
    {
        return new DaemonProcessIdentityAssessment(
            DaemonProcessIdentityAssessmentStatus.Uncertain,
            observedStartTimeUtc.HasValue
                ? ContractArgumentGuard.RequireUtcTimestamp(observedStartTimeUtc.Value, nameof(observedStartTimeUtc))
                : null,
            error ?? throw new ArgumentNullException(nameof(error)));
    }

    /// <summary> Gets the process identity assessment status. </summary>
    public DaemonProcessIdentityAssessmentStatus Status { get; }

    /// <summary> Gets the observed process start time when available. </summary>
    public DateTimeOffset? ObservedStartTimeUtc { get; }

    /// <summary> Gets the structured error when assessment is uncertain or mismatched; otherwise <see langword="null" />. </summary>
    public ExecutionError? Error { get; }

}
