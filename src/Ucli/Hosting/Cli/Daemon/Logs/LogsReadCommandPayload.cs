namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Represents the final payload for <c>logs * read</c>. </summary>
internal sealed record LogsReadCommandPayload (
    int Count,
    string? NextCursor,
    string CompletionReason);
