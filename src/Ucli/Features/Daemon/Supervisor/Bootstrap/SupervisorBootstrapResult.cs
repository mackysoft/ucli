using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;

/// <summary> Represents the result of ensuring one worktree-local supervisor is ready. </summary>
internal sealed record SupervisorBootstrapResult (
    SupervisorInstanceManifest? Manifest,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether bootstrap completed successfully. </summary>
    public bool IsSuccess => Manifest is not null && Error is null;

    /// <summary> Creates one successful bootstrap result. </summary>
    /// <param name="manifest"> The ready supervisor manifest. </param>
    /// <returns> The successful bootstrap result. </returns>
    public static SupervisorBootstrapResult Success (SupervisorInstanceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return new SupervisorBootstrapResult(manifest, null);
    }

    /// <summary> Creates one failed bootstrap result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed bootstrap result. </returns>
    public static SupervisorBootstrapResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new SupervisorBootstrapResult(null, error);
    }
}
