namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Supplies code catalog descriptors owned by one subsystem. </summary>
internal interface ICodeCatalogContributor
{
    /// <summary> Gets the descriptors contributed by this subsystem. </summary>
    /// <returns> The contributed descriptor set. </returns>
    IReadOnlyList<CodeCatalogDescriptor> GetDescriptors ();
}
