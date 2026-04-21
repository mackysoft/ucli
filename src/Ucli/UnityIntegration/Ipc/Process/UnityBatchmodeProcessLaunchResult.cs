using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Represents the result of launching one Unity batchmode child process. </summary>
/// <param name="ProcessHandle"> The started process handle when launch succeeds; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured launch error when launch fails; otherwise <see langword="null" />. </param>
internal sealed record UnityBatchmodeProcessLaunchResult (
    IUnityBatchmodeProcessHandle? ProcessHandle,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether launch succeeded. </summary>
    public bool IsSuccess => ProcessHandle != null && Error is null;

    /// <summary> Creates one successful launch result. </summary>
    /// <param name="processHandle"> The started process handle. </param>
    /// <returns> The successful launch result. </returns>
    public static UnityBatchmodeProcessLaunchResult Success (IUnityBatchmodeProcessHandle processHandle)
    {
        ArgumentNullException.ThrowIfNull(processHandle);
        return new UnityBatchmodeProcessLaunchResult(processHandle, null);
    }

    /// <summary> Creates one failed launch result. </summary>
    /// <param name="error"> The structured launch error. </param>
    /// <returns> The failed launch result. </returns>
    public static UnityBatchmodeProcessLaunchResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityBatchmodeProcessLaunchResult(null, error);
    }
}