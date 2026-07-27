namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents one diagnostic emitted by <c>skills doctor</c>. </summary>
internal sealed record SkillsDoctorDiagnosticCommandPayload (
    UcliSkillDoctorSeverity Severity,
    string Code,
    string Message,
    string? SkillName);
