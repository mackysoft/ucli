using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.completed</c> stream payload. </summary>
public sealed record CompileCompletedEntry
{
    /// <summary> Initializes one compile completion entry for a non-empty execution identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="ExecutionId" /> is empty. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a diagnostic count is negative or <paramref name="Verdict" /> has an undefined value. </exception>
    [JsonConstructor]
    public CompileCompletedEntry (
        Guid ExecutionId,
        Verdict Verdict,
        int ErrorCount,
        int WarningCount)
    {
        if (ExecutionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Lifecycle Execution id must not be empty.",
                nameof(ExecutionId));
        }
        if (!TextVocabulary.IsDefined(Verdict))
        {
            throw new ArgumentOutOfRangeException(nameof(Verdict), Verdict, "Verdict must be defined by the assurance contract.");
        }
        if (ErrorCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ErrorCount), ErrorCount, "Error count must not be negative.");
        }
        if (WarningCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(WarningCount), WarningCount, "Warning count must not be negative.");
        }

        this.ExecutionId = ExecutionId;
        this.Verdict = Verdict;
        this.ErrorCount = ErrorCount;
        this.WarningCount = WarningCount;
    }

    /// <summary> Gets the completed compile Lifecycle Execution identifier. </summary>
    public Guid ExecutionId { get; }

    [JsonInclude]
    [JsonRequired]
    public Verdict Verdict { get; private init; }

    public int ErrorCount { get; }

    public int WarningCount { get; }
}
