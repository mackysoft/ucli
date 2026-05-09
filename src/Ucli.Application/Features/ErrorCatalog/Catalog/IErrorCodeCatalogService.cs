namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

/// <summary> Resolves error-code descriptions with the open-code-set fallback used by public CLI behavior. </summary>
internal interface IErrorCodeCatalogService
{
    /// <summary> Lists known error-code descriptors using optional exact filters. </summary>
    /// <param name="input"> The list filter input. </param>
    /// <returns> A result containing filtered descriptors on success, or an execution error when filters are invalid. </returns>
    ErrorCodeCatalogListResult List (ErrorCodeCatalogListInput input);

    /// <summary> Describes one error code using either a catalog descriptor or the unknown-code fallback. </summary>
    /// <param name="code"> The error code to describe. Invalid values return an invalid-argument failure result. </param>
    /// <param name="requireKnown"> <see langword="true" /> to reject codes absent from the catalog; <see langword="false" /> to return an unknown-code fallback descriptor. </param>
    /// <returns> A result containing a descriptor on success, or an execution error when the input is invalid or unknown codes are forbidden. </returns>
    ErrorCodeCatalogDescribeResult Describe (
        UcliErrorCode code,
        bool requireKnown);
}
