namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

internal interface IErrorCodeCatalogContributor
{
    IReadOnlyList<UcliErrorCodeDescriptor> GetDescriptors ();
}
