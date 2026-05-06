using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents one Unity request execution result. </summary>
internal sealed record UnityRequestExecutionResult
{
    private const string SuccessMessage = "Unity IPC request execution completed.";

    private UnityRequestExecutionResult (
        UnityRequestResponse? response,
        UnityRequestFailure? failure)
    {
        Response = response;
        FailureInfo = failure;
    }

    /// <summary> Gets the host-decoded response on success; otherwise <see langword="null" />. </summary>
    public UnityRequestResponse? Response { get; }

    /// <summary> Gets the classified failure on failure; otherwise <see langword="null" />. </summary>
    public UnityRequestFailure? FailureInfo { get; }

    /// <summary> Gets the user-facing result message. </summary>
    public string Message => FailureInfo?.Message ?? SuccessMessage;

    /// <summary> Gets the machine-readable error code on failure; otherwise <see langword="null" />. </summary>
    public string? ErrorCode => FailureInfo?.Code;

    /// <summary> Gets a value indicating whether request execution succeeded. </summary>
    public bool IsSuccess => Response is not null && FailureInfo is null;

    /// <summary> Creates a successful request-execution result. </summary>
    /// <param name="response"> The host-decoded response returned from Unity. </param>
    /// <returns> The successful request-execution result. </returns>
    public static UnityRequestExecutionResult Success (UnityRequestResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new UnityRequestExecutionResult(
            response,
            failure: null);
    }

    /// <summary> Creates a failed request-execution result. </summary>
    /// <param name="failure"> The classified request failure. </param>
    /// <returns> The failed request-execution result. </returns>
    public static UnityRequestExecutionResult Failure (UnityRequestFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new UnityRequestExecutionResult(
            response: null,
            failure);
    }

    /// <summary> Creates a failed request-execution result. </summary>
    /// <param name="message"> The user-facing failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The failed request-execution result. </returns>
    public static UnityRequestExecutionResult Failure (
        string message,
        string errorCode)
    {
        var normalizedErrorCode = string.IsNullOrWhiteSpace(errorCode)
            ? IpcErrorCodes.InternalError
            : errorCode;
        var normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "Request execution failed."
            : message;

        return Failure(new UnityRequestFailure(
            normalizedErrorCode,
            normalizedMessage,
            ResolveOutcome(normalizedErrorCode)));
    }

    private static ApplicationOutcome ResolveOutcome (string errorCode)
    {
        return errorCode is IpcErrorCodes.InvalidArgument
            or IpcErrorCodes.PlanTokenRequired
            or IpcErrorCodes.PlanTokenInvalid
            or IpcErrorCodes.PlanTokenExpired
            or IpcErrorCodes.PlanTokenRequestMismatch
            or IpcErrorCodes.StateChangedSincePlan
            ? ApplicationOutcome.InvalidArgument
            : ApplicationOutcome.ToolError;
    }
}
