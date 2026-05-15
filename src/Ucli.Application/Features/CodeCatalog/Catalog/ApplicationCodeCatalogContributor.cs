using MackySoft.Ucli.Application.Diagnostics;

namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Adapts application-owned error-code descriptors into the generic code catalog. </summary>
internal sealed class ApplicationCodeCatalogContributor : ICodeCatalogContributor
{
    /// <inheritdoc />
    public IReadOnlyList<CodeCatalogDescriptor> GetDescriptors ()
    {
        return ApplicationErrorCodeDescriptors.All
            .Select(CodeCatalogDescriptorFactory.FromErrorDescriptor)
            .ToArray();
    }
}
