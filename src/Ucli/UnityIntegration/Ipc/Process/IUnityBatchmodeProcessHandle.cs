namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Represents one started Unity batchmode child process. </summary>
internal interface IUnityBatchmodeProcessHandle : IAsyncDisposable
{
    /// <summary> Gets the started child process identifier. </summary>
    int ProcessId { get; }

    /// <summary> Gets the started child process start timestamp in UTC when available. </summary>
    DateTimeOffset? StartTimeUtc { get; }

    /// <summary> Gets a value indicating whether the process has already exited. </summary>
    bool HasExited { get; }

    /// <summary> Gets the child process exit code when the process has already exited; otherwise <see langword="null" />. </summary>
    int? ExitCode { get; }

    /// <summary> Waits until the child process exits. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    Task WaitForExitAsync (CancellationToken cancellationToken = default);

    /// <summary> Terminates the child process and waits until process exit completes. </summary>
    /// <param name="terminationPolicy"> The termination policy. When omitted, force kill is used. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The termination result. </returns>
    Task<ProcessTerminationResult> TerminateAsync (
        ProcessTerminationPolicy? terminationPolicy = null,
        CancellationToken cancellationToken = default);
}
