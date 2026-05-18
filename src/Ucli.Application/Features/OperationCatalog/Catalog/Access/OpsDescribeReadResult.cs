using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents one normalized internal <c>ops describe</c> read result. </summary>
internal sealed record OpsDescribeReadResult (
    OpsDescribeReadOutput? Output,
    string Message,
    UcliCode? ErrorCode,
    StartupFailureDetail? StartupFailure = null)
{
    /// <summary> Gets a value indicating whether the describe read succeeded. </summary>
    public bool IsSuccess => Output is not null && ErrorCode is null;

    /// <summary> Creates a successful describe-read result. </summary>
    public static OpsDescribeReadResult Success (
        OpsDescribeReadOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new OpsDescribeReadResult(output, message, null);
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
