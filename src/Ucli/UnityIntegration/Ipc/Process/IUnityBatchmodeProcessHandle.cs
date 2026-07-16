namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Represents one started Unity batchmode child process. </summary>
internal interface IUnityBatchmodeProcessHandle : IUnityProcessHandle
{
    /// <summary> Gets a value indicating whether the process has already exited. </summary>
    bool HasExited { get; }

    /// <summary> Gets the child process exit code when the process has already exited; otherwise <see langword="null" />. </summary>
    int? ExitCode { get; }

    /// <summary> Waits until the child process exits. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    Task WaitForExitAsync (CancellationToken cancellationToken = default);

}
