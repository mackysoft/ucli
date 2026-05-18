namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

/// <summary> Represents one resolved verify profile. </summary>
internal sealed record VerifyProfileDefinition (
    string Source,
    string Name,
    string? RepositoryRelativePath,
    IReadOnlyList<VerifyProfileStep> Steps);
