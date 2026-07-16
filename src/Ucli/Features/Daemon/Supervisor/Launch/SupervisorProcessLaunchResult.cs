using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Represents one platform supervisor process-launch result. </summary>
internal sealed class SupervisorProcessLaunchResult
{
    private SupervisorProcessLaunchResult (
        ISupervisorProcessLaunchLease? lease,
        ExecutionError? error)
    {
        Lease = lease;
        Error = error;
    }

    /// <summary> Gets the generation-specific lease while cleanup ownership remains with the caller. </summary>
    public ISupervisorProcessLaunchLease? Lease { get; }

    /// <summary> Gets the structured error when launch did not complete successfully. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether launch completed successfully. </summary>
    public bool IsSuccess => Lease is not null && Error is null;

    /// <summary> Creates one successful launch result. </summary>
    /// <param name="lease"> The generation-specific launch lease. </param>
    /// <returns> The successful launch result. </returns>
    public static SupervisorProcessLaunchResult Success (ISupervisorProcessLaunchLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return new SupervisorProcessLaunchResult(lease, null);
    }

    /// <summary> Creates one failed launch result. </summary>
    /// <param name="error"> The structured launch error. </param>
    /// <returns> The failed launch result. </returns>
    public static SupervisorProcessLaunchResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new SupervisorProcessLaunchResult(null, error);
    }

    /// <summary> Creates one failed launch result that still owns a possibly registered process generation. </summary>
    /// <param name="error"> The structured launch error. </param>
    /// <param name="lease"> The unresolved generation-specific launch lease that must be rolled back. </param>
    /// <returns> The failed launch result with unresolved cleanup ownership. </returns>
    public static SupervisorProcessLaunchResult FailureWithLease (
        ExecutionError error,
        ISupervisorProcessLaunchLease lease)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(lease);
        return new SupervisorProcessLaunchResult(lease, error);
    }
}
