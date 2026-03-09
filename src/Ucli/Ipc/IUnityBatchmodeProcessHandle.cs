namespace MackySoft.Ucli.Ipc;

/// <summary> Represents one started Unity batchmode child process. </summary>
internal interface IUnityBatchmodeProcessHandle : IAsyncDisposable
{
    /// <summary> Gets the started child process identifier. </summary>
    int ProcessId { get; }

    /// <summary> Gets a value indicating whether the process has already exited. </summary>
    bool HasExited { get; }

    /// <summary> Gets the child process exit code when the process has already exited; otherwise <see langword="null" />. </summary>
    int? ExitCode { get; }

    /// <summary> Waits until the child process exits. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    Task WaitForExit (CancellationToken cancellationToken = default);

    /// <summary> Terminates the child process and waits until process exit completes. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    Task Terminate (CancellationToken cancellationToken = default);
}
