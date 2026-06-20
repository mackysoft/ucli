using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents one report reference in a verify assurance payload. </summary>
internal sealed record VerifyReportOutput
{
    /// <summary> Gets the optional file path locator. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; init; }

    /// <summary> Gets the optional URI locator. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Uri { get; init; }

    /// <summary> Gets optional integrity metadata. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Digest { get; init; }
}
