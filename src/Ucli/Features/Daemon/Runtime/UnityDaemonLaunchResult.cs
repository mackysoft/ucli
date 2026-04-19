using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Runtime;

/// <summary> Represents the result of launching one Unity batchmode daemon process. </summary>
/// <param name="ProcessId"> The launched process identifier when launch succeeds; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error when launch fails; otherwise <see langword="null" />. </param>
internal sealed record UnityDaemonLaunchResult (
    int? ProcessId,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether launch succeeded. </summary>
    public bool IsSuccess => ProcessId is not null && Error is null;

    /// <summary> Creates a successful launch result. </summary>
    /// <param name="processId"> The launched process identifier. </param>
    /// <returns> The successful launch result. </returns>
    public static UnityDaemonLaunchResult Success (int processId)
    {
        return new UnityDaemonLaunchResult(processId, null);
    }

    /// <summary> Creates a failed launch result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed launch result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UnityDaemonLaunchResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityDaemonLaunchResult(null, error);
    }
}