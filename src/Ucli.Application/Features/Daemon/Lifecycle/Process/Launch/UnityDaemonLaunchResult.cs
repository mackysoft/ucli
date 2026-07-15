using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Launch;

/// <summary> Represents the result of launching one Unity daemon process. </summary>
internal sealed class UnityDaemonLaunchResult
{
    private UnityDaemonLaunchResult (
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        ExecutionError? error)
    {
        ProcessId = processId;
        ProcessStartedAtUtc = processStartedAtUtc;
        Error = error;
    }

    /// <summary> Gets the launched process identifier when launch succeeds. </summary>
    public int? ProcessId { get; }

    /// <summary> Gets the launched process start timestamp when launch succeeds. </summary>
    public DateTimeOffset? ProcessStartedAtUtc { get; }

    /// <summary> Gets the structured error when launch fails. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether launch succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful launch result. </summary>
    /// <param name="processId"> The launched process identifier. </param>
    /// <param name="processStartedAtUtc"> The launched process start timestamp. </param>
    /// <returns> The successful launch result. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="processId" /> is not positive. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="processStartedAtUtc" /> is default or is not UTC. </exception>
    public static UnityDaemonLaunchResult Success (
        int processId,
        DateTimeOffset processStartedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        var validatedProcessStartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            processStartedAtUtc,
            nameof(processStartedAtUtc));

        return new UnityDaemonLaunchResult(processId, validatedProcessStartedAtUtc, error: null);
    }

    /// <summary> Creates a failed launch result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed launch result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UnityDaemonLaunchResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityDaemonLaunchResult(processId: null, processStartedAtUtc: null, error);
    }
}
