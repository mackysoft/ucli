using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents one claim in a verify assurance payload. </summary>
internal sealed record VerifyClaimOutput (
    string Id,
    string Status,
    string Coverage,
    bool Required,
    string VerifierRef,
    string Statement,
    IReadOnlyDictionary<string, object?> Subject,
    IReadOnlyList<VerifyEvidenceOutput> Evidence,
    IReadOnlyList<VerifyResidualRiskOutput> ResidualRisks)
{
    /// <summary> Gets optional claim-validity details, used when verify projects ready claims. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Validity { get; init; }
}
