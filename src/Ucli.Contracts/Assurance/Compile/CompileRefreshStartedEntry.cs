using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.refresh.started</c> stream payload. </summary>
public sealed record CompileRefreshStartedEntry
{
    /// <summary> Initializes one compile refresh entry for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public CompileRefreshStartedEntry (
        Guid RunId,
        string RefreshOrigin,
        string ObservationSource)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.RefreshOrigin = RefreshOrigin;
        this.ObservationSource = ObservationSource;
    }

    public Guid RunId { get; }

    public string RefreshOrigin { get; }

    public string ObservationSource { get; }
}
