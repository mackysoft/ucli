namespace MackySoft.Ucli.Skills.Doctor;

/// <summary> Represents one SKILL doctor diagnostic. </summary>
/// <param name="Severity"> The diagnostic severity. </param>
/// <param name="Code"> The diagnostic code. </param>
/// <param name="Message"> The diagnostic message. </param>
/// <param name="SkillName"> The related skill name, or <see langword="null" /> for target-level diagnostics. </param>
public sealed record SkillDoctorDiagnostic (
    SkillDoctorSeverity Severity,
    string Code,
    string Message,
    string? SkillName = null);
