namespace MackySoft.Ucli.Ops;

/// <summary> Represents the emitted <c>payload.readIndex</c> metadata for <c>ops</c>. </summary>
/// <param name="Used"> Whether the selected result came from persisted read-index. </param>
/// <param name="Hit"> Whether the selected result source produced a usable catalog snapshot. </param>
/// <param name="Source"> The selected result source (<c>index</c> or <c>unity</c>). </param>
/// <param name="Freshness"> The selected result freshness (<c>fresh</c>, <c>probable</c>, or <c>stale</c>). </param>
/// <param name="GeneratedAtUtc"> The selected snapshot generation timestamp. </param>
/// <param name="FallbackReason"> The fallback reason when the source read path was selected. </param>
internal sealed record OpsReadIndexInfo (
    bool Used,
    bool Hit,
    string Source,
    string Freshness,
    DateTimeOffset? GeneratedAtUtc,
    string? FallbackReason);