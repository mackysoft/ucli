using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc;

/// <summary> Represents one Unity IPC request execution result. </summary>
/// <param name="Response"> The IPC response on success; otherwise <see langword="null" />. </param>
/// <param name="Message"> The user-facing result message. </param>
/// <param name="ErrorCode"> The machine-readable error code on failure; otherwise <see langword="null" />. </param>
internal sealed record UnityIpcRequestExecutionResult (
    IpcResponse? Response,
    string Message,
    string? ErrorCode)
{
    /// <summary> Gets a value indicating whether request execution succeeded. </summary>
    public bool IsSuccess => Response is not null && ErrorCode is null;

    /// <summary> Creates a successful request-execution result. </summary>
    /// <param name="response"> The IPC response returned from Unity. </param>
    /// <returns> The successful request-execution result. </returns>
    public static UnityIpcRequestExecutionResult Success (IpcResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new UnityIpcRequestExecutionResult(
            Response: response,
            Message: "Unity IPC request execution completed.",
            ErrorCode: null);
    }

    /// <summary> Creates a failed request-execution result. </summary>
    /// <param name="message"> The user-facing failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The failed request-execution result. </returns>
    public static UnityIpcRequestExecutionResult Failure (
        string message,
        string errorCode)
    {
        return new UnityIpcRequestExecutionResult(
            Response: null,
            Message: message,
            ErrorCode: errorCode);
    }
}