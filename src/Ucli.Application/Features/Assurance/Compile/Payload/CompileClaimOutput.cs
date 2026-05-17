namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents one claim entry in a compile assurance payload. </summary>
internal sealed record CompileClaimOutput (
    string Id,
    string Status,
    string Coverage,
    bool Required,
    string VerifierRef,
    string Statement,
    IReadOnlyDictionary<string, object?> Subject,
    IReadOnlyList<CompileEvidenceOutput> Evidence,
    IReadOnlyList<CompileResidualRiskOutput> ResidualRisks);
