using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("C# eval log entry.")]
public sealed record CsEvalLogEntry
{
    [JsonConstructor]
    public CsEvalLogEntry (
        CsEvalLogLevel level,
        string message)
    {
        if (!TextVocabulary.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "C# eval log level must be specified.");
        }

        Level = level;
        Message = ContractArgumentGuard.RequireValue(message, nameof(message));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Log level.")]
    public CsEvalLogLevel Level { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Log message text.")]
    public string Message { get; private init; }
}
