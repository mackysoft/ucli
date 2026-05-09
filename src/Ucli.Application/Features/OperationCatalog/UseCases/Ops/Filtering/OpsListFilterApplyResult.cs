using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Filtering;

/// <summary> Represents the result of applying compiled <c>ops list</c> filters. </summary>
/// <param name="Operations"> The matched operation entries when successful; otherwise <see langword="null" />. </param>
/// <param name="ErrorMessage"> The filter failure message; otherwise <see langword="null" />. </param>
internal sealed record OpsListFilterApplyResult (
    IReadOnlyList<OpsCatalogListEntry>? Operations,
    string? ErrorMessage)
{
    /// <summary> Gets a value indicating whether filter application succeeded. </summary>
    public bool IsSuccess => Operations is not null && ErrorMessage is null;

    /// <summary> Creates a successful filter-application result. </summary>
    public static OpsListFilterApplyResult Success (IReadOnlyList<OpsCatalogListEntry> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return new OpsListFilterApplyResult(operations, null);
    }

    /// <summary> Creates a failed filter-application result. </summary>
    public static OpsListFilterApplyResult Failure (string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new OpsListFilterApplyResult(null, errorMessage);
    }
}
