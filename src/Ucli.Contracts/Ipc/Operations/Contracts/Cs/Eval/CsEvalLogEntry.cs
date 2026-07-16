using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval log entry.")]
public sealed record CsEvalLogEntry
{
    [JsonConstructor]
    public CsEvalLogEntry (
        CsEvalLogLevel level,
        string message)
    {
        if (!ContractLiteralCodec.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "C# eval log level must be specified.");
        }

        Level = level;
        Message = ContractArgumentGuard.RequireValue(message, nameof(message));
    }

    [UcliRequired]
    [UcliDescription("Log level.")]
    public CsEvalLogLevel Level { get; }

    [UcliRequired]
    [UcliDescription("Log message text.")]
    public string Message { get; }
}
