using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>verify.diagnostic</c> stream payload. </summary>
public sealed record VerifyDiagnosticEntry
{
    /// <summary> Initializes one verify diagnostic entry. </summary>
    [JsonConstructor]
    public VerifyDiagnosticEntry (
        string Code,
        string Message,
        UcliDiagnosticSeverity Severity,
        VerifyStepKind? StepKind)
    {
        if (!TextVocabulary.IsDefined(Severity))
        {
            throw new ArgumentOutOfRangeException(nameof(Severity), Severity, "Diagnostic severity must be defined.");
        }
        if (StepKind.HasValue && !TextVocabulary.IsDefined(StepKind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(StepKind), StepKind, "Verify step kind must be defined when present.");
        }

        this.Code = ContractArgumentGuard.RequireValue(Code, nameof(Code));
        this.Message = ContractArgumentGuard.RequireValue(Message, nameof(Message));
        this.Severity = Severity;
        this.StepKind = StepKind;
    }

    public string Code { get; }

    public string Message { get; }

    public UcliDiagnosticSeverity Severity { get; }

    public VerifyStepKind? StepKind { get; }
}
