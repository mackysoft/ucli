using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents one verifier entry in a verify assurance payload. </summary>
internal sealed record VerifyVerifierOutput (
    string Id,
    string Kind,
    bool Deterministic,
    bool Required,
    IReadOnlyList<string> PrimaryClaims,
    IReadOnlyList<string> Effects)
{
    /// <summary> Gets the optional report reference produced by this verifier. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReportRef { get; init; }
}
