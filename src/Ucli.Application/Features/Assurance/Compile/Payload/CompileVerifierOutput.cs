namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents one verifier entry in a compile assurance payload. </summary>
internal sealed record CompileVerifierOutput (
    string Id,
    string Kind,
    bool Deterministic,
    bool Required,
    IReadOnlyList<string> PrimaryClaims,
    IReadOnlyList<string> Effects,
    string ReportRef);
