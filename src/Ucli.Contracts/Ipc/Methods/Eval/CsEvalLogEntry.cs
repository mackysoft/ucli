using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("C# eval log entry.")]
public sealed record CsEvalLogEntry
{
    [JsonConstructor]
    public CsEvalLogEntry (
        long sequence,
        CsEvalLogLevel level,
        string message,
        JsonElement? data)
    {
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        if (!TextVocabulary.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "C# eval log level must be specified.");
        }

        if (data.HasValue && data.Value.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("C# eval log data must be a JSON value.", nameof(data));
        }

        Sequence = sequence;
        Level = level;
        Message = ContractArgumentGuard.RequireValue(message, nameof(message));
        Data = data?.Clone();
    }

    [JsonInclude]
    [JsonRequired]
    [Description("One-based call-local log sequence.")]
    public long Sequence { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Log level.")]
    public CsEvalLogLevel Level { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Log message text.")]
    public string Message { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Optional structured log data.")]
    public JsonElement? Data { get; private init; }
}
