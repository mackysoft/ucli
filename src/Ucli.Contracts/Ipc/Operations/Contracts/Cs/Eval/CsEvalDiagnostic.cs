using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval compile or entry point diagnostic.")]
public sealed record CsEvalDiagnostic
{
    [JsonConstructor]
    public CsEvalDiagnostic (
        string severity,
        string id,
        string message,
        int? line,
        int? column)
    {
        Severity = severity;
        Id = id;
        Message = message;
        Line = line;
        Column = column;
    }

    [UcliRequired]
    [UcliDescription("Diagnostic severity literal.")]
    public string Severity { get; init; }

    [UcliRequired]
    [UcliDescription("Diagnostic identifier.")]
    public string Id { get; init; }

    [UcliRequired]
    [UcliDescription("Diagnostic message.")]
    public string Message { get; init; }

    [UcliDescription("One-based source line when available.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Line { get; init; }

    [UcliDescription("One-based source column when available.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Column { get; init; }
}
