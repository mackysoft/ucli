namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents application read-index metadata for reader commands. </summary>
/// <param name="Used"> Whether the selected result came from persisted read-index metadata. </param>
/// <param name="Hit"> Whether a usable persisted read-index snapshot was found. </param>
/// <param name="Source"> The selected result source. </param>
/// <param name="Freshness"> The selected result freshness. </param>
/// <param name="GeneratedAtUtc"> The selected snapshot generation timestamp. </param>
/// <param name="FallbackReason"> The fallback reason when persisted metadata could not be used fully. </param>
internal sealed record ReadIndexInfo (
    bool Used,
    bool Hit,
    ReadIndexInfoSource Source,
    IndexFreshness Freshness,
    DateTimeOffset? GeneratedAtUtc,
    string? FallbackReason);
