using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Preflight;

/// <summary> Represents one preflight outcome for ops execution. </summary>
internal sealed record OpsPreflightResult
{
    private OpsPreflightResult (
        OpsPreflightContext? context,
        string message,
        UcliCode? errorCode)
    {
        if (context is null)
        {
            ArgumentNullException.ThrowIfNull(errorCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
        }
        else if (errorCode is not null)
        {
            throw new ArgumentException("Successful preflight must not contain an error code.", nameof(errorCode));
        }

        Context = context;
        Message = message;
        ErrorCode = errorCode;
    }

    public OpsPreflightContext? Context { get; }

    public string Message { get; }

    public UcliCode? ErrorCode { get; }

    /// <summary> Gets a value indicating whether preflight succeeded. </summary>
    public bool IsSuccess => Context is not null;

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
