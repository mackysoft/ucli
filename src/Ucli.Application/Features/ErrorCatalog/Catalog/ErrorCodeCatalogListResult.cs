using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

/// <summary> Represents the result of listing known error-code descriptors. </summary>
/// <param name="Descriptors"> The filtered descriptors on success, or <see langword="null" /> on failure. </param>
/// <param name="Error"> The failure to project when listing failed. </param>
internal sealed record ErrorCodeCatalogListResult (
    IReadOnlyList<UcliErrorCodeDescriptor>? Descriptors,
    ExecutionError? Error)
{
    /// <summary> Gets whether the result contains descriptors and no failure error. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful list result. </summary>
    /// <param name="descriptors"> The filtered descriptors. </param>
    /// <returns> A success result with no execution error. </returns>
    public static ErrorCodeCatalogListResult Success (IReadOnlyList<UcliErrorCodeDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        return new ErrorCodeCatalogListResult(descriptors, Error: null);
    }

    /// <summary> Creates a failed list result. </summary>
    /// <param name="error"> The execution error to return. </param>
    /// <returns> A failure result with no descriptors. </returns>
    public static ErrorCodeCatalogListResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ErrorCodeCatalogListResult(Descriptors: null, error);
    }
}
