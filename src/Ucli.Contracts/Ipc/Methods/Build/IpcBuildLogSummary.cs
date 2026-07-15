using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the build log artifact summary. </summary>
public sealed record IpcBuildLogSummary
{
    /// <summary> Initializes one build log artifact summary. </summary>
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

        this.EntryCount = EntryCount;
        this.ErrorCount = ErrorCount;
        this.WarningCount = WarningCount;
        this.CompletionReason = CompletionReason;
        this.Window = ContractArgumentGuard.RequireNotNull(Window, nameof(Window));
    }

    public int EntryCount { get; }

    public int ErrorCount { get; }

    public int WarningCount { get; }

    public IpcBuildLogCompletionReason CompletionReason { get; }

    public IpcBuildLogWindow Window { get; }
}
