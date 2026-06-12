namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents one claim entry in a build assurance payload. </summary>
internal sealed record BuildClaimOutput (
    string Id,
    string Status,
    string Coverage,
    bool Required,
    string VerifierRef,
    string Statement,
    IReadOnlyDictionary<string, object?> Subject,
    IReadOnlyList<BuildEvidenceOutput> Evidence,
    IReadOnlyList<BuildResidualRiskOutput> ResidualRisks);
