namespace MackySoft.Ucli.Skills.Doctor;

/// <summary> Represents a SKILL doctor result. </summary>
/// <param name="Host"> The diagnosed host. </param>
/// <param name="TargetRoot"> The diagnosed target root. </param>
/// <param name="Diagnostics"> The diagnostics. </param>
public sealed record SkillDoctorResult (
    string Host,
    string TargetRoot,
    IReadOnlyList<SkillDoctorDiagnostic> Diagnostics)
{
    /// <summary> Gets a value indicating whether no error diagnostics were reported. </summary>
    public bool IsHealthy => Diagnostics.All(static diagnostic => diagnostic.Severity != SkillDoctorSeverity.Error);
}
