using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one non-fatal diagnostic attached to a public execute step result. </summary>
/// <param name="Code"> The stable diagnostic code. </param>
/// <param name="Severity"> The diagnostic severity. </param>
/// <param name="CoverageImpact"> The diagnostic coverage impact. </param>
/// <param name="Message"> The human-readable diagnostic message. </param>
public sealed record IpcExecuteDiagnostic
{
    /// <summary> Initializes one diagnostic. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="Message" /> is empty or whitespace. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Code" /> or <paramref name="Message" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when an enum value is not defined by the contract. </exception>
    [JsonConstructor]
    public IpcExecuteDiagnostic (
        UcliCode Code,
        UcliDiagnosticSeverity Severity,
        IpcExecuteDiagnosticCoverageImpact CoverageImpact,
        string Message)
    {
        if (!ContractLiteralCodec.IsDefined(Severity))
        {
            throw new ArgumentOutOfRangeException(nameof(Severity), Severity, "Diagnostic severity must be specified.");
        }

        if (!ContractLiteralCodec.IsDefined(CoverageImpact))
        {
            throw new ArgumentOutOfRangeException(nameof(CoverageImpact), CoverageImpact, "Diagnostic coverage impact must be specified.");
        }

        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        this.Severity = Severity;
        this.CoverageImpact = CoverageImpact;
        this.Message = ContractArgumentGuard.RequireValue(Message, nameof(Message));
    }

    public UcliCode Code { get; }

    public UcliDiagnosticSeverity Severity { get; }

    public IpcExecuteDiagnosticCoverageImpact CoverageImpact { get; }

    public string Message { get; }
}
