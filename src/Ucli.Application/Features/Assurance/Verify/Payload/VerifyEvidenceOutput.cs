using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents one evidence entry in a verify claim. </summary>
internal sealed record VerifyEvidenceOutput (
    string Kind)
{
    /// <summary> Gets an optional report reference. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EvidenceRef { get; init; }

    /// <summary> Gets optional inline evidence data. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; init; }
}
