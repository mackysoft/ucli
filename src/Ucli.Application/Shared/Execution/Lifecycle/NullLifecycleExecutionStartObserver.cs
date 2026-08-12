using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary> Represents callers that do not own an additional durable record beyond Lifecycle Execution. </summary>
internal sealed class NullLifecycleExecutionStartObserver : ILifecycleExecutionStartObserver
{
    public static NullLifecycleExecutionStartObserver Instance { get; } = new();

    private NullLifecycleExecutionStartObserver () { }

    public ValueTask<LifecycleExecutionStartObservation> ObserveAsync (LifecycleExecutionStartBinding start)
    {
        ArgumentNullException.ThrowIfNull(start);
        return ValueTask.FromResult<LifecycleExecutionStartObservation>(
            LifecycleExecutionStartObservation.Observed.Instance);
    }
}
