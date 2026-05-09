using MackySoft.Ucli.Application.Diagnostics;

namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

internal sealed class ApplicationErrorCodeCatalogContributor : IErrorCodeCatalogContributor
{
    public IReadOnlyList<UcliErrorCodeDescriptor> GetDescriptors ()
    {
        return ApplicationErrorCodeDescriptors.All;
    }
}
