using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents one evidence entry in a build assurance claim. </summary>
internal sealed record BuildEvidenceOutput (
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    BuildArtifactKind? EvidenceRef,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    object? Data);
