namespace MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

/// <summary> Represents one normalized <c>ops list</c> service result. </summary>
/// <param name="Output"> The successful output; otherwise <see langword="null" />. </param>
/// <param name="Message"> The user-facing result message. </param>
/// <param name="ErrorCode"> The machine-readable error code on failure; otherwise <see langword="null" />. </param>
/// <param name="StartupFailure"> The structured startup failure detail when source fallback failed before Unity accepted the request. </param>
internal sealed record OpsListServiceResult (
    OpsListExecutionOutput? Output,
    string Message,
    UcliCode? ErrorCode,
    StartupFailureDetail? StartupFailure = null)
{
    /// <summary> Gets a value indicating whether the service execution succeeded. </summary>
    public bool IsSuccess => Output is not null && ErrorCode is null;

    /// <summary> Creates a successful service result. </summary>
    /// <param name="output"> The successful output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static OpsListServiceResult Success (
        OpsListExecutionOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new OpsListServiceResult(output, message, null);
    }

    /// <summary> Creates a failed service result. </summary>
    /// <param name="message"> The failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The failed result. </returns>
    public static OpsListServiceResult Failure (
        string message,
        UcliCode errorCode,
        StartupFailureDetail? startupFailure = null)
    {
        return new OpsListServiceResult(null, message, errorCode, startupFailure);
    }
}
