namespace MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

/// <summary> Defines residual risk codes emitted by the <c>build.run</c> command. </summary>
internal static class BuildRiskCodes
{
    public static readonly UcliCode ProjectMutationDetected = new("BUILD_PROJECT_MUTATION_DETECTED");

    public static readonly UcliCode ProjectMutationAuditCoverageIncomplete = new("BUILD_PROJECT_MUTATION_AUDIT_COVERAGE_INCOMPLETE");

    public static IReadOnlyList<UcliCode> All { get; } =
    [
        ProjectMutationDetected,
        ProjectMutationAuditCoverageIncomplete,
    ];
}
