using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Represents either one validated supervisor IPC request or its terminal validation error. </summary>
internal sealed class SupervisorIpcRequestValidationResult
{
    private SupervisorIpcRequestValidationResult (
        ValidatedSupervisorIpcRequest? request,
        IpcResponse? errorResponse,
        IpcResponseMode responseMode)
    {
        Request = request;
        ErrorResponse = errorResponse;
        ResponseMode = responseMode;
    }

    /// <summary> Gets whether validation produced a dispatchable request. </summary>
    public bool IsSuccess => Request is not null;

    /// <summary> Gets the validated request, or <see langword="null" /> when validation failed. </summary>
    public ValidatedSupervisorIpcRequest? Request { get; }

    /// <summary> Gets the terminal validation error, or <see langword="null" /> when validation succeeded. </summary>
    public IpcResponse? ErrorResponse { get; }

    /// <summary> Gets the response framing mode selected while validating the envelope. </summary>
    public IpcResponseMode ResponseMode { get; }

    /// <summary> Creates a successful validation result. </summary>
    public static SupervisorIpcRequestValidationResult Success (ValidatedSupervisorIpcRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new SupervisorIpcRequestValidationResult(request, null, request.ResponseMode);
    }

    /// <summary> Creates a failed validation result with the framing mode used to return the error. </summary>
    public static SupervisorIpcRequestValidationResult Failure (
        IpcResponse errorResponse,
        IpcResponseMode responseMode)
    {
        ArgumentNullException.ThrowIfNull(errorResponse);
        if (!TextVocabulary.IsDefined(responseMode))
        {
            throw new ArgumentOutOfRangeException(nameof(responseMode), responseMode, "IPC response mode must be defined.");
        }

        return new SupervisorIpcRequestValidationResult(null, errorResponse, responseMode);
    }
}
