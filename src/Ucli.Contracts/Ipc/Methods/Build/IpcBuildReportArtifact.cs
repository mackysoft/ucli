using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the normalized Unity BuildReport artifact. </summary>
public sealed record IpcBuildReportArtifact
{
    /// <summary> Initializes one normalized Unity BuildReport artifact. </summary>
    /// <param name="SchemaVersion"> The schema version, which must be <c>1</c>. </param>
    /// <param name="Result"> The defined BuildReport result, including <see cref="IpcBuildReportResult.Unknown" /> when Unity cannot classify it. </param>
    /// <param name="UnityBuildTarget"> The non-empty Unity build target. </param>
    /// <param name="OutputPath"> The output path, which may be empty but must not be <see langword="null" />. </param>
    /// <param name="DurationMilliseconds"> The non-negative build duration in milliseconds. </param>
    /// <param name="TotalSizeBytes"> The non-negative total output size in bytes. </param>
    /// <param name="ErrorCount"> The non-negative error count. </param>
    /// <param name="WarningCount"> The non-negative warning count. </param>
    /// <param name="Steps"> The normalized steps, containing no <see langword="null" /> items. </param>
    /// <param name="Messages"> The normalized messages, containing no <see langword="null" /> items. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required string or collection is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the build target is empty or a collection contains a <see langword="null" /> item. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the schema version or result is unsupported, or a summary value is negative. </exception>
    [JsonConstructor]
    public IpcBuildReportArtifact (
        int SchemaVersion,
        IpcBuildReportResult Result,
        string UnityBuildTarget,
        string OutputPath,
        long DurationMilliseconds,
        long TotalSizeBytes,
        int ErrorCount,
        int WarningCount,
        IReadOnlyList<IpcBuildReportStep> Steps,
        IReadOnlyList<IpcBuildReportMessage> Messages)
    {
        if (SchemaVersion != 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SchemaVersion),
                SchemaVersion,
                "Build report schema version must be 1.");
        }

        if (!TextVocabulary.IsDefined(Result))
        {
            throw new ArgumentOutOfRangeException(nameof(Result), Result, "Build report result must be specified.");
        }

        if (DurationMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DurationMilliseconds),
                DurationMilliseconds,
                "Build report duration must not be negative.");
        }

        if (TotalSizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TotalSizeBytes),
                TotalSizeBytes,
                "Build report total size must not be negative.");
        }

        this.SchemaVersion = SchemaVersion;
        this.Result = Result;
        this.UnityBuildTarget = ContractArgumentGuard.RequireValue(UnityBuildTarget, nameof(UnityBuildTarget));
        this.OutputPath = ContractArgumentGuard.RequireNotNull(OutputPath, nameof(OutputPath));
        this.DurationMilliseconds = DurationMilliseconds;
        this.TotalSizeBytes = TotalSizeBytes;
        this.ErrorCount = ContractArgumentGuard.RequireNonNegative(ErrorCount, nameof(ErrorCount));
        this.WarningCount = ContractArgumentGuard.RequireNonNegative(WarningCount, nameof(WarningCount));
        this.Steps = ContractArgumentGuard.RequireItems(Steps, nameof(Steps));
        this.Messages = ContractArgumentGuard.RequireItems(Messages, nameof(Messages));
    }

    public int SchemaVersion { get; }

    public IpcBuildReportResult Result { get; }

    public string UnityBuildTarget { get; }

    public string OutputPath { get; }

    public long DurationMilliseconds { get; }

    public long TotalSizeBytes { get; }

    public int ErrorCount { get; }

    public int WarningCount { get; }

    public IReadOnlyList<IpcBuildReportStep> Steps { get; }

    public IReadOnlyList<IpcBuildReportMessage> Messages { get; }
}
