using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents one <c>build.diagnostic</c> stream payload. </summary>
public sealed record BuildDiagnosticEntry
{
    /// <summary> Initializes one build diagnostic for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public BuildDiagnosticEntry (
        Guid RunId,
        UcliCode Code,
        UcliDiagnosticSeverity Severity,
        string Message,
        BuildRunProgressPhase Phase)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        if (!TextVocabulary.IsDefined(Severity))
        {
            throw new ArgumentOutOfRangeException(nameof(Severity), Severity, "Build diagnostic severity must be specified.");
        }

        if (!TextVocabulary.IsDefined(Phase))
        {
            throw new ArgumentOutOfRangeException(nameof(Phase), Phase, "Build diagnostic phase must be specified.");
        }

        this.RunId = RunId;
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        this.Severity = Severity;
        this.Message = ContractArgumentGuard.RequireValue(Message, nameof(Message));
        this.Phase = Phase;
    }

    public Guid RunId { get; }

    public UcliCode Code { get; }

    public UcliDiagnosticSeverity Severity { get; }

    public string Message { get; }

    public BuildRunProgressPhase Phase { get; }
}
