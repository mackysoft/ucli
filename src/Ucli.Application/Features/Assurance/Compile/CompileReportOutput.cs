using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Represents one compile report reference. </summary>
internal sealed record CompileReportOutput (
    string Kind,
    string Path,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Digest = null);
