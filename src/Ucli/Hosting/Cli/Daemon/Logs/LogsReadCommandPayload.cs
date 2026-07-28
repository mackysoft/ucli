using System.Text.Json.Serialization;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Represents the final payload for <c>logs * read</c>. </summary>
/// <param name="Count"> The number of entries emitted before completion. </param>
/// <param name="NextCursor"> The latest cursor confirmed by the read flow. </param>
/// <param name="CompletionReason"> The reason the read flow completed. </param>
/// <param name="ActionRequired"> The optional recovery action. </param>
internal sealed record LogsReadCommandPayload (
    int Count,
    string? NextCursor,
    LogsReadCompletionReason CompletionReason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    LogsReadActionRequired? ActionRequired)
    : CommandErrorPayload<LogsReadCommandPayload>;
