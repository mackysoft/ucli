using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the build log artifact summary. </summary>
public sealed record IpcBuildLogSummary
{
    /// <summary> Initializes one build log artifact summary. </summary>
    /// <param name="EntryCount"> The non-negative log entry count. </param>
    /// <param name="ErrorCount"> The non-negative error count. </param>
    /// <param name="WarningCount"> The non-negative warning count. </param>
    /// <param name="CompletionReason"> The defined reason log capture completed. </param>
    /// <param name="Window"> The timestamp and cursor window used for log capture. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Window" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the completion reason is undefined or a count is negative. </exception>
    [JsonConstructor]
    public IpcBuildLogSummary (
        int EntryCount,
        int ErrorCount,
        int WarningCount,
        IpcBuildLogCompletionReason CompletionReason,
        IpcBuildLogWindow Window)
    {
        if (!ContractLiteralCodec.IsDefined(CompletionReason))
        {
            throw new ArgumentOutOfRangeException(nameof(CompletionReason), CompletionReason, "Build log completion reason must be specified.");
        }

        this.EntryCount = ContractArgumentGuard.RequireNonNegative(EntryCount, nameof(EntryCount));
        this.ErrorCount = ContractArgumentGuard.RequireNonNegative(ErrorCount, nameof(ErrorCount));
        this.WarningCount = ContractArgumentGuard.RequireNonNegative(WarningCount, nameof(WarningCount));
        this.CompletionReason = CompletionReason;
        this.Window = ContractArgumentGuard.RequireNotNull(Window, nameof(Window));
    }

    public int EntryCount { get; }

    public int ErrorCount { get; }

    public int WarningCount { get; }

    public IpcBuildLogCompletionReason CompletionReason { get; }

    public IpcBuildLogWindow Window { get; }
}
