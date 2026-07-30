namespace MackySoft.Ucli.Application.Features.Assurance;

/// <summary> Provides report identities emitted by the built-in assurance workflows. </summary>
internal static class AssuranceReportIds
{
    public static AssuranceReportId CompileSummary { get; } = new("compile.summary");

    public static AssuranceReportId CompileDiagnostics { get; } = new("compile.diagnostics");

    public static AssuranceReportId TestSummary { get; } = new("test.summary");

    public static AssuranceReportId UnityLogs { get; } = new("logs.unity");
}
