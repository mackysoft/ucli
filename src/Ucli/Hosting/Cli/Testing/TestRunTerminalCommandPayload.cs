using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Hosting.Cli.Testing;

/// <summary> Defines the only Test Run state the current command can establish. </summary>
[VocabularyDefinition]
internal enum TestRunCompletedState
{
    [VocabularyText("completed")]
    Completed = 1,
}

/// <summary> Represents a completed Test Run with a valid result-set verdict. </summary>
internal sealed record TestRunCompletedCommandPayload : IVerdictResult
{
    public TestRunCompletedCommandPayload (
        Verdict verdict,
        Guid runId,
        string artifactsDir,
        string summaryJsonPath)
    {
        if (!TextVocabulary.IsDefined(verdict))
        {
            throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "Verdict must be defined.");
        }

        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Test-run identifier must not be empty.", nameof(runId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(artifactsDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(summaryJsonPath);
        State = TestRunCompletedState.Completed;
        Verdict = verdict;
        RunId = runId;
        ArtifactsDir = artifactsDir;
        SummaryJsonPath = summaryJsonPath;
    }

    /// <summary> Gets the completed state established by this payload type. </summary>
    public TestRunCompletedState State { get; }

    /// <summary> Gets the verdict established from the complete normalized result set. </summary>
    public Verdict Verdict { get; }

    /// <summary> Gets the completed Test Run identifier. </summary>
    public Guid RunId { get; }

    /// <summary> Gets the directory containing the completed Test Run artifacts. </summary>
    public string ArtifactsDir { get; }

    /// <summary> Gets the path of the completed summary artifact. </summary>
    public string SummaryJsonPath { get; }
}
