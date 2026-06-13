namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the build log artifact summary. </summary>
/// <param name="EntryCount"> The number of normalized log entries counted in the build window. </param>
/// <param name="ErrorCount"> The number of raw build log window entries classified as errors. </param>
/// <param name="WarningCount"> The number of raw build log window entries classified as warnings. </param>
/// <param name="CompletionReason"> The normalized build log completion reason. </param>
/// <param name="Window"> The build log time window. </param>
public sealed record IpcBuildLogSummary (
    int EntryCount,
    int ErrorCount,
    int WarningCount,
    string CompletionReason,
    IpcBuildLogWindow Window);
