using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval log entry.")]
public sealed record CsEvalLogEntry
{
    [JsonConstructor]
    public CsEvalLogEntry (
        string level,
        string message)
    {
        Level = level;
        Message = message;
    }

    [UcliRequired]
    [UcliDescription("Log level literal.")]
    public string Level { get; init; }

    [UcliRequired]
    [UcliDescription("Log message text.")]
    public string Message { get; init; }
}
