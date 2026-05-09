using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

/// <summary>
/// <para> Represents the result of describing one error code. </para>
/// <para> Successful unknown-code lookups have <see cref="Known" /> set to <see langword="false" /> and still carry a fallback descriptor. </para>
/// </summary>
/// <param name="Descriptor"> The descriptor to project on success, or <see langword="null" /> on failure. </param>
/// <param name="Known"> Whether <paramref name="Descriptor" /> came from the catalog rather than the unknown-code fallback. </param>
/// <param name="Error"> The failure to project when description failed. </param>
internal sealed record ErrorCodeCatalogDescribeResult (
    UcliErrorCodeDescriptor? Descriptor,
    bool Known,
    ExecutionError? Error)
{
    /// <summary> Gets whether the result contains a descriptor and no failure error. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful description result. </summary>
    /// <param name="descriptor"> The descriptor to return. </param>
    /// <param name="known"> Whether <paramref name="descriptor" /> came from the catalog. </param>
    /// <returns> A success result with no execution error. </returns>
    public static ErrorCodeCatalogDescribeResult Success (
        UcliErrorCodeDescriptor descriptor,
        bool known)
    {
        return new ErrorCodeCatalogDescribeResult(descriptor, known, Error: null);
    }

    /// <summary> Creates a failed description result. </summary>
    /// <param name="error"> The execution error to return. </param>
    /// <returns> A failure result with no descriptor. </returns>
    public static ErrorCodeCatalogDescribeResult Failure (ExecutionError error)
    {
        return new ErrorCodeCatalogDescribeResult(Descriptor: null, Known: false, error);
    }
}
