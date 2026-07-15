namespace MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

/// <summary> Represents one normalized <c>ops describe</c> service result. </summary>
internal sealed record OpsDescribeServiceResult
{
    private OpsDescribeServiceResult (
        OpsDescribeExecutionOutput? output,
        string message,
        UcliCode? errorCode,
        StartupFailureDetail? startupFailure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (output is null)
        {
            ArgumentNullException.ThrowIfNull(errorCode);
        }
        else
        {
            if (errorCode is not null)
            {
                throw new ArgumentException("Successful service result must not contain an error code.", nameof(errorCode));
            }

            if (startupFailure is not null)
            {
                throw new ArgumentException("Successful service result must not contain startup failure details.", nameof(startupFailure));
            }
        }

        Output = output;
        Message = message;
        ErrorCode = errorCode;
        StartupFailure = startupFailure;
    }

    public OpsDescribeExecutionOutput? Output { get; }

    public string Message { get; }

    public UcliCode? ErrorCode { get; }

    public StartupFailureDetail? StartupFailure { get; }

    /// <summary> Gets a value indicating whether the service execution succeeded. </summary>
    public bool IsSuccess => Output is not null;

    /// <summary> Creates a successful service result. </summary>
    /// <param name="output"> The successful output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static OpsDescribeServiceResult Success (
        OpsDescribeExecutionOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new OpsDescribeServiceResult(output, message, null, null);
    }

    /// <summary> Creates a failed service result. </summary>
    /// <param name="message"> The failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The failed result. </returns>
    public static OpsDescribeServiceResult Failure (
        string message,
        UcliCode errorCode,
        StartupFailureDetail? startupFailure = null)
    {
        return new OpsDescribeServiceResult(null, message, errorCode, startupFailure);
    }
}
