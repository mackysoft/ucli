namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents one normalized internal <c>ops list</c> read result. </summary>
internal sealed record OpsListReadResult
{
    private OpsListReadResult (
        OpsListReadOutput? output,
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
                throw new ArgumentException("Successful list read must not contain an error code.", nameof(errorCode));
            }

            if (startupFailure is not null)
            {
                throw new ArgumentException("Successful list read must not contain startup failure details.", nameof(startupFailure));
            }
        }

        Output = output;
        Message = message;
        ErrorCode = errorCode;
        StartupFailure = startupFailure;
    }

    public OpsListReadOutput? Output { get; }

    public string Message { get; }

    public UcliCode? ErrorCode { get; }

    public StartupFailureDetail? StartupFailure { get; }

    /// <summary> Gets a value indicating whether the list read succeeded. </summary>
    public bool IsSuccess => Output is not null;

    /// <summary> Creates a successful list-read result. </summary>
    public static OpsListReadResult Success (
        OpsListReadOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new OpsListReadResult(output, message, null, null);
    }

    /// <summary> Creates a failed list-read result. </summary>
    public static OpsListReadResult Failure (
        string message,
        UcliCode errorCode,
        StartupFailureDetail? startupFailure = null)
    {
        return new OpsListReadResult(null, message, errorCode, startupFailure);
    }
}
