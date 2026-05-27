using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one read-index artifact observation. </summary>
internal sealed record ReadyReadIndexArtifactOutput (
    string Name,
    string Status,
    bool Required = true,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Freshness = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SourceInputsHash = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? GeneratedAtUtc = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Code = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Message = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ActionRequired = null);
