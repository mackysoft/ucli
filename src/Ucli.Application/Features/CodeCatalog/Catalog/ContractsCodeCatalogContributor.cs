namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Adapts contract-owned error-code descriptors into the generic code catalog. </summary>
internal sealed class ContractsCodeCatalogContributor : ICodeCatalogContributor
{
    /// <inheritdoc />
    public IReadOnlyList<CodeCatalogDescriptor> GetDescriptors ()
    {
        return UcliKnownErrorCodeDescriptors.All
            .Select(CodeCatalogDescriptorFactory.FromErrorDescriptor)
            .ToArray();
    }
}
