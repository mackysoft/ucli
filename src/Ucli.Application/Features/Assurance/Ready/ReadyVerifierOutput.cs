namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one verifier entry in a ready assurance payload. </summary>
internal sealed record ReadyVerifierOutput (
    string Id,
    string Kind,
    bool Deterministic,
    bool Required,
    IReadOnlyList<string> PrimaryClaims,
    IReadOnlyList<string> Effects);
