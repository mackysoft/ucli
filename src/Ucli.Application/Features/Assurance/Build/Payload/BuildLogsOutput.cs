namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the build log artifact summary. </summary>
internal sealed record BuildLogsOutput (
    string ReportRef,
    int EntryCount,
    int ErrorCount,
    int WarningCount,
    string CompletionReason,
    BuildLogWindowOutput Window);
