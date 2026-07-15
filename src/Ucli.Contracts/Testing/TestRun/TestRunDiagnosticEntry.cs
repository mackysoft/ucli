using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents the <c>test.run.diagnostic</c> stream payload. </summary>
public sealed record TestRunDiagnosticEntry
{
    /// <summary> Initializes one validated test-run diagnostic. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty or <paramref name="Message" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Code" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Severity" /> is not a defined diagnostic severity. </exception>
    [JsonConstructor]
    public TestRunDiagnosticEntry (
        Guid RunId,
        UcliCode Code,
        string Message,
        UcliDiagnosticSeverity Severity)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        if (!ContractLiteralCodec.IsDefined(Severity))
        {
            throw new ArgumentOutOfRangeException(nameof(Severity), Severity, "Test-run diagnostic severity must be specified.");
        }

        this.RunId = RunId;
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        this.Message = ContractArgumentGuard.RequireValue(Message, nameof(Message));
        this.Severity = Severity;
    }

    public Guid RunId { get; }

    public UcliCode Code { get; }

    public string Message { get; }

    public UcliDiagnosticSeverity Severity { get; }
}
