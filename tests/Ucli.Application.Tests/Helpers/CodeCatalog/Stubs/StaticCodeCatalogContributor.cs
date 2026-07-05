using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StaticCodeCatalogContributor : ICodeCatalogContributor
{
    private readonly IReadOnlyList<CodeCatalogDescriptor> descriptors;

    public StaticCodeCatalogContributor (IEnumerable<CodeCatalogDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        this.descriptors = descriptors.ToArray();
    }

    public IReadOnlyList<CodeCatalogDescriptor> GetDescriptors ()
    {
        return descriptors;
    }
}
