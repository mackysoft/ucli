using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents the result of probing Unity lifecycle readiness. </summary>
internal sealed record ReadyLifecycleProbeResult (
    ReadyLifecycleOutput? Lifecycle,
    UnityReadinessDecision Decision,
    ApplicationFailure? Failure)
{
    /// <summary> Gets a value indicating whether lifecycle probing produced a readiness decision. </summary>
    public bool IsSuccess => Failure is null;

    /// <summary> Creates a successful probe result. </summary>
    public static ReadyLifecycleProbeResult Success (
        ReadyLifecycleOutput? lifecycle,
        UnityReadinessDecision decision)
    {
        return new ReadyLifecycleProbeResult(
            lifecycle,
            decision,
            Failure: null);
    }

    /// <summary> Creates a failed command-level probe result. </summary>
    public static ReadyLifecycleProbeResult FailureResult (ApplicationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new ReadyLifecycleProbeResult(
            Lifecycle: null,
            Decision: UnityReadinessDecision.Failure(failure.Code, failure.Message),
            failure);
    }
}
