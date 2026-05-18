using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents one residual risk in a verify assurance payload. </summary>
internal sealed record VerifyResidualRiskOutput (
    string Code,
    bool Blocking)
{
    /// <summary> Gets an optional human-readable risk message. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
}
