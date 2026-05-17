namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one claim entry in a ready assurance payload. </summary>
internal sealed record ReadyClaimOutput (
    string Id,
    string Status,
    string Coverage,
    bool Required,
    string VerifierRef,
    string Statement,
    IReadOnlyDictionary<string, object?> Subject,
    ReadyClaimValidityOutput Validity,
    IReadOnlyList<ReadyEvidenceOutput> Evidence,
    IReadOnlyList<ReadyResidualRiskOutput> ResidualRisks);
