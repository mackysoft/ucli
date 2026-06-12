using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents one evidence entry in a build assurance claim. </summary>
internal sealed record BuildEvidenceOutput (
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? EvidenceRef = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    object? Data = null);
