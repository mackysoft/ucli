namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents one verifier entry in a build assurance payload. </summary>
internal sealed record BuildVerifierOutput (
    string Id,
    string Kind,
    bool Deterministic,
    bool Required,
    IReadOnlyList<string> PrimaryClaims,
    IReadOnlyList<string> Effects,
    string ReportRef);
