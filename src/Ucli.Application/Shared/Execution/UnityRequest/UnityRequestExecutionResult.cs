using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents one Unity request execution result. </summary>
/// <param name="Response"> The host-decoded response on success; otherwise <see langword="null" />. </param>
/// <param name="Message"> The user-facing result message. </param>
/// <param name="ErrorCode"> The machine-readable error code on failure; otherwise <see langword="null" />. </param>
internal sealed record UnityRequestExecutionResult (
    UnityRequestResponse? Response,
    string Message,
    UcliErrorCode? ErrorCode)
{
    /// <summary> Gets a value indicating whether request execution succeeded. </summary>
    public bool IsSuccess => Response is not null && ErrorCode is null;

    /// <summary> Creates a successful request-execution result. </summary>
    /// <param name="response"> The host-decoded response returned from Unity. </param>
    /// <returns> The successful request-execution result. </returns>
    public static UnityRequestExecutionResult Success (UnityRequestResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new UnityRequestExecutionResult(
            Response: response,
            Message: "Unity IPC request execution completed.",
            ErrorCode: null);
    }

    /// <summary> Creates a failed request-execution result. </summary>
    /// <param name="message"> The user-facing failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The failed request-execution result. </returns>
    public static UnityRequestExecutionResult Failure (
        string message,
        UcliErrorCode? errorCode)
    {
        return new UnityRequestExecutionResult(
            Response: null,
            Message: message,
            ErrorCode: errorCode);
    }
}
