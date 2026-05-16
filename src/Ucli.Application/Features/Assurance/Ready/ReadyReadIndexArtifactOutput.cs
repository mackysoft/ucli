namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one read-index artifact observation. </summary>
internal sealed record ReadyReadIndexArtifactOutput (
    string Name,
    string Status,
    string? Freshness = null,
    string? SourceInputsHash = null,
    DateTimeOffset? GeneratedAtUtc = null,
    string? Code = null,
    string? Message = null);
