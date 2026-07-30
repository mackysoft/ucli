using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents raw build log window counters and completion metadata. </summary>
internal sealed record BuildLogsOutput
{
    public BuildLogsOutput (
        int EntryCount,
        int ErrorCount,
        int WarningCount,
        IpcBuildLogCompletionReason CompletionReason,
        BuildLogWindowOutput Window)
    {
        if (!TextVocabulary.IsDefined(CompletionReason))
        {
            throw new ArgumentOutOfRangeException(nameof(CompletionReason), CompletionReason, "Build log completion reason must be specified.");
        }

        this.EntryCount = EntryCount;
        this.ErrorCount = ErrorCount;
        this.WarningCount = WarningCount;
        this.CompletionReason = CompletionReason;
        this.Window = Window;
    }

    public BuildArtifactKind ReportRef { get; } = BuildArtifactKind.BuildLog;

    public int EntryCount { get; }

    public int ErrorCount { get; }

    public int WarningCount { get; }

    public IpcBuildLogCompletionReason CompletionReason { get; }

    public BuildLogWindowOutput Window { get; }
}
