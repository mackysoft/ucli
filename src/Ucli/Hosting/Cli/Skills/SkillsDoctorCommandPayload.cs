using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents the payload emitted by <c>skills doctor</c>. </summary>
internal sealed record SkillsDoctorCommandPayload (
    UcliOfficialSkillHost Host,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> SkillNames,
    UcliSkillScope Scope,
    string? RepositoryRoot,
    string TargetRoot,
    string ReloadGuidance,
    bool IsHealthy,
    IReadOnlyList<SkillsDoctorDiagnosticCommandPayload> Diagnostics)
    : CommandErrorPayload<SkillsDoctorCommandPayload>;
