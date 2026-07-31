using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("C# eval compile or entry point diagnostic.")]
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

    [JsonInclude]
    [JsonRequired]
    [Description("Diagnostic severity.")]
    public UcliDiagnosticSeverity Severity { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Diagnostic identifier.")]
    public string Id { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Diagnostic message.")]
    public string Message { get; private init; }

    [Description("One-based source line when available.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Line { get; }

    [Description("One-based source column when available.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Column { get; }
}
