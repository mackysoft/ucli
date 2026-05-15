using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary>
/// <para> Represents the result of describing one code value. </para>
/// <para> Successful unknown-code lookups have <see cref="Known" /> set to <see langword="false" /> and still carry a fallback descriptor. </para>
/// </summary>
/// <param name="Descriptor"> The descriptor to project on success, or <see langword="null" /> on failure. </param>
/// <param name="Known"> Whether <paramref name="Descriptor" /> came from the catalog rather than the unknown-code fallback. </param>
/// <param name="Error"> The failure to project when description failed. </param>
internal sealed record CodeCatalogDescribeResult (
    CodeCatalogDescriptor? Descriptor,
    bool Known,
    ExecutionError? Error)
{
    /// <summary> Gets whether the result contains a descriptor and no failure error. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful description result. </summary>
    /// <param name="descriptor"> The descriptor to return. </param>
    /// <param name="known"> Whether <paramref name="descriptor" /> came from the catalog. </param>
    /// <returns> A success result with no execution error. </returns>
    public static CodeCatalogDescribeResult Success (
        CodeCatalogDescriptor descriptor,
        bool known)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new CodeCatalogDescribeResult(descriptor, known, Error: null);
    }

    /// <summary> Creates a failed description result. </summary>
    /// <param name="error"> The execution error to return. </param>
    /// <returns> A failure result with no descriptor. </returns>
    public static CodeCatalogDescribeResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new CodeCatalogDescribeResult(Descriptor: null, Known: false, error);
    }
}
