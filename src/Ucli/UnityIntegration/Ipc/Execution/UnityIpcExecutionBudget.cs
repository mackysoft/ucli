using MackySoft.Ucli.Application.Shared.Execution.Timeout;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Tracks the shared timeout budget consumed by one Unity IPC execution path. </summary>
internal sealed class UnityIpcExecutionBudget
{
    private readonly ExecutionDeadline deadline;

    private UnityIpcExecutionBudget (
        TimeSpan timeout,
        ExecutionDeadline deadline)
    {
        Timeout = timeout;
        this.deadline = deadline;
    }

    /// <summary> Gets the original timeout budget. </summary>
    public TimeSpan Timeout { get; }

    /// <summary> Starts one timeout budget. </summary>
    /// <param name="timeout"> The total timeout budget. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="timeProvider"> The time provider used to observe elapsed time. </param>
    /// <returns> The started timeout budget. </returns>
    public static UnityIpcExecutionBudget Start (
        TimeSpan timeout,
        TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new UnityIpcExecutionBudget(
            timeout,
            ExecutionDeadline.Start(timeout, timeProvider));
    }

    /// <summary> Tries to get the remaining timeout in this budget. </summary>
    /// <param name="remainingTimeout"> The remaining timeout when available; otherwise <see cref="TimeSpan.Zero" />. </param>
    /// <returns> <see langword="true" /> when positive timeout remains; otherwise <see langword="false" />. </returns>
    public bool TryGetRemainingTimeout (out TimeSpan remainingTimeout)
    {
        return deadline.TryGetRemainingTimeout(out remainingTimeout);
    }
}
