namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

internal interface IErrorCodeCatalogService
{
    ErrorCodeCatalogDescribeResult Describe (
        UcliErrorCode code,
        bool requireKnown);
}
