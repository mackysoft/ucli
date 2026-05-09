using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

internal sealed record ErrorCodeCatalogDescribeResult (
    UcliErrorCodeDescriptor? Descriptor,
    bool Known,
    ExecutionError? Error)
{
    public bool IsSuccess => Error is null;

    public static ErrorCodeCatalogDescribeResult Success (
        UcliErrorCodeDescriptor descriptor,
        bool known)
    {
        return new ErrorCodeCatalogDescribeResult(descriptor, known, Error: null);
    }

    public static ErrorCodeCatalogDescribeResult Failure (ExecutionError error)
    {
        return new ErrorCodeCatalogDescribeResult(Descriptor: null, Known: false, error);
    }
}
