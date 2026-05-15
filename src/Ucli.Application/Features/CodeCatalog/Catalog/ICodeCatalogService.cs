namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Provides code catalog list and describe use cases. </summary>
internal interface ICodeCatalogService
{
    /// <summary> Lists catalog descriptors matching the supplied filters. </summary>
    /// <param name="input"> The list filters. </param>
    /// <returns> The filtered list result. </returns>
    CodeCatalogListResult List (CodeCatalogListInput input);

    /// <summary> Describes one code value, optionally requiring a known catalog entry. </summary>
    /// <param name="reference"> The code lookup reference. </param>
    /// <param name="requireKnown"> <see langword="true" /> to reject codes absent from the catalog; <see langword="false" /> to return an unknown-code fallback descriptor. </param>
    /// <returns> The description lookup result. </returns>
    CodeCatalogDescribeResult Describe (
        CodeCatalogCodeReference reference,
        bool requireKnown);
}
