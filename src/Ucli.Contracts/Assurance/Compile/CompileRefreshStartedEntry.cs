using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.refresh.started</c> stream payload. </summary>
public sealed record CompileRefreshStartedEntry
{
    /// <summary> Initializes one observed compile refresh entry for a non-empty execution identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="ExecutionId" /> is empty. </exception>
    [JsonConstructor]
    public CompileRefreshStartedEntry (
        Guid ExecutionId,
        CompileLifecycleRefreshOrigin RefreshOrigin,
        string ObservationSource)
    {
        if (ExecutionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Lifecycle Execution id must not be empty.",
                nameof(ExecutionId));
        }
        if (!TextVocabulary.IsDefined(RefreshOrigin))
        {
            throw new ArgumentOutOfRangeException(nameof(RefreshOrigin), RefreshOrigin, "Compile refresh origin must be defined.");
        }

        this.ExecutionId = ExecutionId;
        this.RefreshOrigin = RefreshOrigin;
        this.ObservationSource = ObservationSource;
    }

    public Guid ExecutionId { get; }

    public CompileLifecycleRefreshOrigin RefreshOrigin { get; }

    public string ObservationSource { get; }
}
