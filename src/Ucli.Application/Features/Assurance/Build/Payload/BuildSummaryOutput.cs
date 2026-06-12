namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the BuildReport-derived build summary. </summary>
internal sealed record BuildSummaryOutput (
    string Result,
    long DurationMilliseconds,
    int ErrorCount,
    int WarningCount,
    string ReportRef);
