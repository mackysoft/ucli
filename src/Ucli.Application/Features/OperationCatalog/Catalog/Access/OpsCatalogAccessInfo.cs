using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents internal access metadata produced by ops catalog reads. </summary>
/// <param name="Used"> Whether the selected result came from persisted read-index. </param>
/// <param name="Hit"> Whether the selected result source produced a usable catalog snapshot. </param>
/// <param name="Source"> The selected result source. </param>
/// <param name="Freshness"> The selected result freshness. </param>
/// <param name="GeneratedAtUtc"> The selected snapshot generation timestamp. </param>
/// <param name="FallbackReason"> The fallback reason when the source read path was selected. </param>
internal sealed record OpsCatalogAccessInfo (
    bool Used,
    bool Hit,
    OpsCatalogSource Source,
    IndexFreshness Freshness,
    DateTimeOffset? GeneratedAtUtc,
    string? FallbackReason);
