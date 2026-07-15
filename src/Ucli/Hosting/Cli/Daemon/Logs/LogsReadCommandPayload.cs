using System.Text.Json.Serialization;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Represents the final payload for <c>logs * read</c>. </summary>
internal sealed record LogsReadCommandPayload
{
    /// <summary> Initializes the serialized command payload from a validated completion reason. </summary>
    public LogsReadCommandPayload (
        int count,
        string? nextCursor,
        LogsReadCompletionReason completionReason,
        string? actionRequired)
    {
        Count = count;
        NextCursor = nextCursor;
        CompletionReason = ContractLiteralCodec.ToValue(completionReason);
        ActionRequired = actionRequired;
    }

    /// <summary> Gets the number of entries emitted before completion. </summary>
    public int Count { get; }

    /// <summary> Gets the latest cursor confirmed by the read flow. </summary>
    public string? NextCursor { get; }

    /// <summary> Gets the serialized completion-reason literal. </summary>
    public string CompletionReason { get; }

    /// <summary> Gets the optional recovery action. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActionRequired { get; }
}
