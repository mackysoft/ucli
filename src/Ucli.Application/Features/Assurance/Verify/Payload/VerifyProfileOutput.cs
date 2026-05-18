namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents the effective verify profile identity. </summary>
internal sealed record VerifyProfileOutput (
    string Source,
    string Name,
    string? Path,
    string Digest);
