using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval compile or entry point diagnostic.")]
public sealed record CsEvalDiagnostic
{
    [JsonConstructor]
    public CsEvalDiagnostic (
        UcliDiagnosticSeverity severity,
        string id,
        string message,
        int? line,
        int? column)
    {
        if (!TextVocabulary.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "C# eval diagnostic severity must be specified.");
        }

        if (line.HasValue != column.HasValue)
        {
            throw new ArgumentException("C# eval diagnostic line and column must either both be specified or both be omitted.");
        }

        Severity = severity;
        Id = ContractArgumentGuard.RequireValue(id, nameof(id));
        Message = ContractArgumentGuard.RequireValue(message, nameof(message));
        Line = line.HasValue
            ? ContractArgumentGuard.RequirePositive(line.Value, nameof(line))
            : null;
        Column = column.HasValue
            ? ContractArgumentGuard.RequirePositive(column.Value, nameof(column))
            : null;
    }

    [UcliRequired]
    [UcliDescription("Diagnostic severity.")]
    public UcliDiagnosticSeverity Severity { get; }

    [UcliRequired]
    [UcliDescription("Diagnostic identifier.")]
    public string Id { get; }

    [UcliRequired]
    [UcliDescription("Diagnostic message.")]
    public string Message { get; }

    [UcliDescription("One-based source line when available.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Line { get; }

    [UcliDescription("One-based source column when available.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Column { get; }
}
