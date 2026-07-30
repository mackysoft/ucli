using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Preflight;

/// <summary> Represents one preflight outcome for ops execution. </summary>
internal sealed record OpsPreflightResult
{
    private OpsPreflightResult (
        OpsPreflightContext? context,
        ApplicationFailure? failure)
    {
        if (context is null)
        {
            ArgumentNullException.ThrowIfNull(failure);
        }
        else if (failure is not null)
        {
            throw new ArgumentException("Successful preflight must not contain a failure.", nameof(failure));
        }

        Context = context;
        Error = failure;
    }

    public OpsPreflightContext? Context { get; }

    public ApplicationFailure? Error { get; }

    /// <summary> Gets a value indicating whether preflight succeeded. </summary>
    public bool IsSuccess => Context is not null;

    /// <summary> Creates one successful preflight result. </summary>
    /// <param name="context"> The resolved execution context. </param>
    /// <returns> The successful preflight result. </returns>
    public static OpsPreflightResult Success (OpsPreflightContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new OpsPreflightResult(context, null);
    }

    /// <summary> Creates one failed preflight result. </summary>
    /// <param name="failure"> The classified application failure. </param>
    /// <returns> The failed preflight result. </returns>
    public static OpsPreflightResult Failure (ApplicationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new OpsPreflightResult(null, failure);
    }
}
