namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Provides validated lookup access to the aggregated code descriptor catalog. </summary>
internal interface ICodeCatalog
{
    /// <summary> Gets descriptors sorted by code value using ordinal comparison. </summary>
    IReadOnlyList<CodeCatalogDescriptor> Descriptors { get; }

    /// <summary> Tries to find the descriptor registered for a code value. </summary>
    /// <param name="code"> The code value to resolve. Unknown values are treated as not found. </param>
    /// <param name="descriptor"> The descriptor registered for <paramref name="code" /> when the method returns <see langword="true" />. </param>
    /// <returns> <see langword="true" /> when the catalog contains <paramref name="code" />; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="code" /> is <see langword="null" />. </exception>
    bool TryFind (
        UcliCode code,
        out CodeCatalogDescriptor descriptor);
}
