namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

/// <summary> Provides validated lookup access to the aggregated error-code descriptor catalog. </summary>
internal interface IErrorCodeCatalog
{
    /// <summary> Gets descriptors sorted by <see cref="UcliErrorCode.Value" /> using ordinal comparison. </summary>
    IReadOnlyList<UcliErrorCodeDescriptor> Descriptors { get; }

    /// <summary> Tries to find the descriptor registered for an error code. </summary>
    /// <param name="code"> The error code to resolve. Invalid or unknown values are treated as not found. </param>
    /// <param name="descriptor"> The descriptor registered for <paramref name="code" /> when the method returns <see langword="true" />. </param>
    /// <returns> <see langword="true" /> when the catalog contains <paramref name="code" />; otherwise <see langword="false" />. </returns>
    bool TryFind (
        UcliErrorCode code,
        out UcliErrorCodeDescriptor descriptor);
}
