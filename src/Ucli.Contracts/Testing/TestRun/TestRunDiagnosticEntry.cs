using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents the <c>test.run.diagnostic</c> stream payload. </summary>
public sealed record TestRunDiagnosticEntry
{
    /// <summary> Initializes one test-run diagnostic for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public TestRunDiagnosticEntry (
        Guid RunId,
        string Code,
        string Message,
        string Severity)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.Code = Code;
        this.Message = Message;
        this.Severity = Severity;
    }

    public Guid RunId { get; }

    public string Code { get; }

    public string Message { get; }

    public string Severity { get; }
}
