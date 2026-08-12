using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

/// <summary>
/// Makes observation of a provider-confirmed Lifecycle Start Record single-flight across response
/// recovery and transport retries for one logical dispatch.
/// </summary>
internal sealed class LifecycleExecutionStartObserverGate
{
    private readonly ILifecycleExecutionStartObserver? observer;

    private readonly object syncRoot = new();

    private LifecycleExecutionStartBinding? observedStart;

    private Task<LifecycleExecutionStartObservation>? observation;

    public LifecycleExecutionStartObserverGate (ILifecycleExecutionStartObserver? observer)
    {
        this.observer = observer;
    }

    public ValueTask<LifecycleExecutionStartObservation> ObserveAsync (
        LifecycleExecutionStartBinding start)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (observer is null)
        {
            return ValueTask.FromResult<LifecycleExecutionStartObservation>(
                LifecycleExecutionStartObservation.Observed.Instance);
        }

        lock (syncRoot)
        {
            if (observedStart is not null && !HasSameBinding(observedStart, start))
            {
                throw new InvalidOperationException(
                    "Lifecycle Execution dispatch observed different durable Start Record bindings.");
            }

            observedStart ??= start;
            observation ??= ObserveCoreAsync(start);
            return new ValueTask<LifecycleExecutionStartObservation>(observation);
        }
    }

    private async Task<LifecycleExecutionStartObservation> ObserveCoreAsync (
        LifecycleExecutionStartBinding start)
    {
        try
        {
            return await observer!.ObserveAsync(start).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return new LifecycleExecutionStartObservation.Rejected(
                ApplicationFailure.InternalError(
                    "Persisting the provider-confirmed Lifecycle Execution start failed. "
                    + exception.Message));
        }
    }

    private static bool HasSameBinding (
        LifecycleExecutionStartBinding left,
        LifecycleExecutionStartBinding right)
    {
        return left.LifecycleExecutionRef == right.LifecycleExecutionRef
            && left.Project == right.Project
            && left.Host == right.Host
            && left.StartedGeneration == right.StartedGeneration
            && left.DeadlineUtc == right.DeadlineUtc
            && left.StartedAtUtc == right.StartedAtUtc;
    }
}
