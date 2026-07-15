namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents one normalized internal <c>ops describe</c> read result. </summary>
internal sealed record OpsDescribeReadResult
{
    private OpsDescribeReadResult (
        OpsDescribeReadOutput? output,
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
                throw new ArgumentException("Successful describe read must not contain an error code.", nameof(errorCode));
            }

            if (startupFailure is not null)
            {
                throw new ArgumentException("Successful describe read must not contain startup failure details.", nameof(startupFailure));
            }
        }

        Output = output;
        Message = message;
        ErrorCode = errorCode;
        StartupFailure = startupFailure;
    }

    public OpsDescribeReadOutput? Output { get; }

    public string Message { get; }

    public UcliCode? ErrorCode { get; }

    public StartupFailureDetail? StartupFailure { get; }

    /// <summary> Gets a value indicating whether the describe read succeeded. </summary>
    public bool IsSuccess => Output is not null;

    /// <summary> Creates a successful describe-read result. </summary>
    public static OpsDescribeReadResult Success (
        OpsDescribeReadOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new OpsDescribeReadResult(output, message, null, null);
    }

    /// <summary> Creates a failed describe-read result. </summary>
    public static OpsDescribeReadResult Failure (
        string message,
        UcliCode errorCode,
        StartupFailureDetail? startupFailure = null)
    {
        return new OpsDescribeReadResult(null, message, errorCode, startupFailure);
    }
}
