namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

/// <summary> Represents one operation result consumed by post-read verification. </summary>
internal sealed record VerifyFromOperationResult (
    int Index,
    string Op,
    bool Applied,
    bool Changed,
    int TouchedCount,
    IReadOnlyList<VerifyFromDiagnostic> Diagnostics,
    VerifyFromPostReadSourceStep PostReadSource);
