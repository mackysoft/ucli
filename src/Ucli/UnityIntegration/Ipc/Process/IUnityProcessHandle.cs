namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Represents one started Unity child process whose ownership can be transferred or reclaimed. </summary>
internal interface IUnityProcessHandle : IAsyncDisposable
{
    /// <summary> Gets the started child process identifier. </summary>
    int ProcessId { get; }

    /// <summary> Gets the started child process start timestamp in UTC when available. </summary>
    DateTimeOffset? StartTimeUtc { get; }

    /// <summary> Terminates the child process and waits until process exit completes. </summary>
    /// <param name="terminationPolicy"> The required termination policy. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The termination result. </returns>
    Task<ProcessTerminationResult> TerminateAsync (
        ProcessTerminationPolicy terminationPolicy,
        CancellationToken cancellationToken);
}
