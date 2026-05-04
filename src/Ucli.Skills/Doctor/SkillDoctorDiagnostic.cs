namespace MackySoft.Ucli.Skills.Doctor;

/// <summary> Represents one SKILL doctor diagnostic. </summary>
public sealed record SkillDoctorDiagnostic
{
    private SkillDoctorDiagnostic (
        SkillDoctorSeverity severity,
        string code,
        string message,
        string? skillName)
    {
        Severity = severity;
        Code = code;
        Message = message;
        SkillName = skillName;
    }

    /// <summary> Gets the diagnostic severity. </summary>
    public SkillDoctorSeverity Severity { get; }

    /// <summary> Gets the diagnostic code. </summary>
    public string Code { get; }

    /// <summary> Gets the diagnostic message. </summary>
    public string Message { get; }

    /// <summary> Gets the related skill name, or <see langword="null" /> for target-level diagnostics. </summary>
    public string? SkillName { get; }

    /// <summary> Creates an error diagnostic. </summary>
    /// <param name="code"> The diagnostic code. </param>
    /// <param name="message"> The diagnostic message. </param>
    /// <param name="skillName"> The related skill name, or <see langword="null" /> for target-level diagnostics. </param>
    /// <returns> The error diagnostic. </returns>
    public static SkillDoctorDiagnostic Error (
        string code,
        string message,
        string? skillName = null)
    {
        return Create(SkillDoctorSeverity.Error, code, message, skillName);
    }

    /// <summary> Creates an informational diagnostic. </summary>
    /// <param name="code"> The diagnostic code. </param>
    /// <param name="message"> The diagnostic message. </param>
    /// <param name="skillName"> The related skill name, or <see langword="null" /> for target-level diagnostics. </param>
    /// <returns> The informational diagnostic. </returns>
    public static SkillDoctorDiagnostic Info (
        string code,
        string message,
        string? skillName = null)
    {
        return Create(SkillDoctorSeverity.Info, code, message, skillName);
    }

    private static SkillDoctorDiagnostic Create (
        SkillDoctorSeverity severity,
        string code,
        string message,
        string? skillName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new SkillDoctorDiagnostic(severity, code, message, skillName);
    }
}
