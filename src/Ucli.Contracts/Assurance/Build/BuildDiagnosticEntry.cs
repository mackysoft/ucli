using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents one <c>build.diagnostic</c> stream payload. </summary>
public sealed record BuildDiagnosticEntry
{
    /// <summary> Initializes one build diagnostic for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public BuildDiagnosticEntry (
        Guid RunId,
        string Code,
        string Severity,
        string Message,
        string Phase)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.Code = Code;
        this.Severity = Severity;
        this.Message = Message;
        this.Phase = Phase;
    }

    public Guid RunId { get; }

    public string Code { get; }

    public string Severity { get; }

    public string Message { get; }

    public string Phase { get; }
}
