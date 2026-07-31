namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary>
/// Defines the caller-side completion window reserved after a Lifecycle Execution deadline.
/// </summary>
internal static class LifecycleExecutionTiming
{
    /// <summary>
    /// Gets the time reserved for deadline terminalization, immutable publication, and response delivery.
    /// </summary>
    public static readonly TimeSpan ResponseDeliveryGrace = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Adds the shared response-delivery grace without overflowing the supported duration.
    /// </summary>
    /// <param name="executionTimeout"> The positive action execution timeout. </param>
    /// <returns> The caller wait timeout including terminalization and delivery grace. </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="executionTimeout" /> is not positive.
    /// </exception>
    public static TimeSpan AddResponseDeliveryGrace (TimeSpan executionTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            executionTimeout,
            TimeSpan.Zero);
        return TimeSpan.MaxValue - executionTimeout < ResponseDeliveryGrace
            ? TimeSpan.MaxValue
            : executionTimeout + ResponseDeliveryGrace;
    }
}
