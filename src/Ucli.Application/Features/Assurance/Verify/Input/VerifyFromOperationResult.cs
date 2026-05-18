namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

/// <summary> Represents one operation result consumed by post-read verification. </summary>
internal sealed record VerifyFromOperationResult (
    string OpId,
    string Op,
    bool Applied,
    bool Changed,
    int TouchedCount,
    IReadOnlyList<VerifyFromDiagnostic> Diagnostics);
