namespace MackySoft.Ucli.Shared.Execution.ReadIndex;

/// <summary> Represents the emitted <c>payload.readIndex</c> metadata for reader commands. </summary>
/// <param name="Used"> Whether the selected result came from persisted read-index metadata. </param>
/// <param name="Hit"> Whether a usable persisted read-index snapshot was found. </param>
/// <param name="Source"> The selected result source (<c>index</c> or <c>unity</c>). </param>
/// <param name="Freshness"> The selected result freshness (<c>fresh</c>, <c>probable</c>, or <c>stale</c>). </param>
/// <param name="GeneratedAtUtc"> The selected snapshot generation timestamp. </param>
/// <param name="FallbackReason"> The fallback reason when persisted metadata could not be used fully. </param>
internal sealed record ReadIndexInfo (
    bool Used,
    bool Hit,
    string Source,
    string Freshness,
    DateTimeOffset? GeneratedAtUtc,
    string? FallbackReason);
