namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Owns one started detached process generation until its lifetime is transferred or reclaimed. </summary>
internal interface IDetachedProcessHandle : IAsyncDisposable
{
    /// <summary> Terminates the owned process generation and waits for its exit. </summary>
    /// <param name="terminationPolicy"> The required termination policy. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The termination result. </returns>
    Task<ProcessTerminationResult> TerminateAsync (
        ProcessTerminationPolicy terminationPolicy,
        CancellationToken cancellationToken);
}
