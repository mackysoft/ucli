namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

/// <summary> Adapts the contract-owned descriptor aggregate into the application catalog contributor boundary. </summary>
internal sealed class ContractsErrorCodeCatalogContributor : IErrorCodeCatalogContributor
{
    /// <inheritdoc />
    public IReadOnlyList<UcliErrorCodeDescriptor> GetDescriptors ()
    {
        return UcliKnownErrorCodeDescriptors.All;
    }
}
