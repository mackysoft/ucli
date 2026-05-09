namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents one normalized internal <c>ops list</c> read result. </summary>
internal sealed record OpsListReadResult (
    OpsListReadOutput? Output,
    string Message,
    UcliErrorCode? ErrorCode)
{
    /// <summary> Gets a value indicating whether the list read succeeded. </summary>
    public bool IsSuccess => Output is not null && ErrorCode is null;

    /// <summary> Creates a successful list-read result. </summary>
    public static OpsListReadResult Success (
        OpsListReadOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new OpsListReadResult(output, message, null);
    }

    /// <summary> Creates a failed list-read result. </summary>
    public static OpsListReadResult Failure (
        string message,
        UcliErrorCode errorCode)
    {
        return new OpsListReadResult(null, message, errorCode);
    }
}
