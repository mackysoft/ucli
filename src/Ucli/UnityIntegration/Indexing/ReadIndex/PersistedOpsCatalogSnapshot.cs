using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

/// <summary> Represents one persisted ops-catalog snapshot together with observed freshness. </summary>
/// <param name="Entries"> The persisted operation entries. </param>
/// <param name="GeneratedAtUtc"> The snapshot generation timestamp. </param>
/// <param name="Freshness"> The observed snapshot freshness. </param>
internal sealed record PersistedOpsCatalogSnapshot (
    IReadOnlyList<IndexOpEntryJsonContract> Entries,
    DateTimeOffset GeneratedAtUtc,
    IndexFreshness Freshness);
