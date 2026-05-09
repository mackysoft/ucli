using MackySoft.Ucli.Application.Diagnostics;

namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

/// <summary> Adapts the application-owned descriptor aggregate into the catalog contributor boundary. </summary>
internal sealed class ApplicationErrorCodeCatalogContributor : IErrorCodeCatalogContributor
{
    /// <inheritdoc />
    public IReadOnlyList<UcliErrorCodeDescriptor> GetDescriptors ()
    {
        return ApplicationErrorCodeDescriptors.All;
    }
}
