namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents raw build log window counters and completion metadata. </summary>
internal sealed record BuildLogsOutput (
    string ReportRef,
    int EntryCount,
    int ErrorCount,
    int WarningCount,
    string CompletionReason,
    BuildLogWindowOutput Window);
