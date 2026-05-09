using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Launch;

/// <summary> Represents the result of launching one Unity batchmode daemon process. </summary>
/// <param name="ProcessId"> The launched process identifier when launch succeeds; otherwise <see langword="null" />. </param>
/// <param name="ProcessStartedAtUtc"> The launched process start timestamp when launch succeeds; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error when launch fails; otherwise <see langword="null" />. </param>
internal sealed record UnityDaemonLaunchResult (
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether launch succeeded. </summary>
    public bool IsSuccess => ProcessId is not null && ProcessStartedAtUtc is not null && Error is null;

    /// <summary> Creates a successful launch result. </summary>
    /// <param name="processId"> The launched process identifier. </param>
    /// <param name="processStartedAtUtc"> The launched process start timestamp. </param>
    /// <returns> The successful launch result. </returns>
    public static UnityDaemonLaunchResult Success (
        int processId,
        DateTimeOffset processStartedAtUtc)
    {
        return new UnityDaemonLaunchResult(processId, processStartedAtUtc, null);
    }

    /// <summary> Creates a failed launch result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed launch result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UnityDaemonLaunchResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityDaemonLaunchResult(null, null, error);
    }
}
