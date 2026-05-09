namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

internal sealed class ContractsErrorCodeCatalogContributor : IErrorCodeCatalogContributor
{
    public IReadOnlyList<UcliErrorCodeDescriptor> GetDescriptors ()
    {
        return UcliKnownErrorCodeDescriptors.All;
    }
}
