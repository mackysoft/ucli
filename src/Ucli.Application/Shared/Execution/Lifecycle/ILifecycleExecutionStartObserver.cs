using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary>
/// Persists the provider-confirmed durable start of one Lifecycle Execution before its action is
/// allowed to enter the provider.
/// </summary>
internal interface ILifecycleExecutionStartObserver
{
    /// <summary>
    /// Records the re-read durable Start Record. The observer is never canceled merely because
    /// the caller that initiated the wait has left.
    /// </summary>
    /// <param name="start"> The provider-confirmed, re-read Start Record binding. </param>
    /// <returns> The closed result that permits or rejects action dispatch. </returns>
    ValueTask<LifecycleExecutionStartObservation> ObserveAsync (
        LifecycleExecutionStartBinding start);
}

/// <summary> Represents the closed outcome of durable-start observation. </summary>
internal abstract record LifecycleExecutionStartObservation
{
    private LifecycleExecutionStartObservation ()
    {
    }

    /// <summary> Permits the associated Lifecycle Execution action to be dispatched. </summary>
    internal sealed record Observed : LifecycleExecutionStartObservation
    {
        /// <summary> Gets the singleton successful observation result. </summary>
        public static Observed Instance { get; } = new();

        private Observed ()
        {
        }
    }

    /// <summary> Prevents action dispatch after durable-start persistence failed. </summary>
    internal sealed record Rejected : LifecycleExecutionStartObservation
    {
        /// <summary> Initializes a rejected observation. </summary>
        public Rejected (ApplicationFailure failure)
        {
            Failure = failure ?? throw new ArgumentNullException(nameof(failure));
        }

        /// <summary> Gets the failure that prevented action dispatch. </summary>
        public ApplicationFailure Failure { get; }
    }
}
