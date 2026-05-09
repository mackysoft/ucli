namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

/// <summary> Supplies one fixed set of error-code descriptors to the application catalog aggregate. </summary>
internal interface IErrorCodeCatalogContributor
{
    /// <summary> Gets descriptors owned by one catalog source. </summary>
    /// <returns> A non-<see langword="null" /> descriptor list. Empty means the source contributes no codes. </returns>
    IReadOnlyList<UcliErrorCodeDescriptor> GetDescriptors ();
}
