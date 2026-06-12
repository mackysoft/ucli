using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents one build report reference. </summary>
internal sealed record BuildReportOutput (
    string Kind,
    string Path,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Digest = null);
