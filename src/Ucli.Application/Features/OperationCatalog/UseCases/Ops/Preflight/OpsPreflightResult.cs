using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Preflight;

/// <summary> Represents one preflight outcome for ops execution. </summary>
/// <param name="Context"> The resolved execution context when preflight succeeds; otherwise <see langword="null" />. </param>
/// <param name="Message"> The user-facing failure message; otherwise an empty string. </param>
/// <param name="ErrorCode"> The machine-readable failure code; otherwise <see langword="null" />. </param>
internal sealed record OpsPreflightResult (
    OpsPreflightContext? Context,
    string Message,
    UcliCode? ErrorCode)
{
    /// <summary> Gets a value indicating whether preflight succeeded. </summary>
    public bool IsSuccess => Context is not null && ErrorCode is null;

    /// <summary> Creates one successful preflight result. </summary>
    /// <param name="context"> The resolved execution context. </param>
    /// <returns> The successful preflight result. </returns>
    public static OpsPreflightResult Success (OpsPreflightContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new OpsPreflightResult(context, string.Empty, null);
    }

    /// <summary> Creates one failed preflight result. </summary>
    /// <param name="message"> The failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The failed preflight result. </returns>
    public static OpsPreflightResult Failure (
        string message,
        UcliCode errorCode)
    {
        return new OpsPreflightResult(null, message, errorCode);
    }
}
