namespace MackySoft.Ucli.Hosting.Cli.Common.Streaming;

/// <summary> Represents one command progress entry before CLI stream formatting is applied. </summary>
/// <param name="EventName"> The command-specific progress event name. </param>
/// <typeparam name="TPayload"> The command-specific progress payload type. </typeparam>
/// <param name="Payload"> The command-specific progress payload. </param>
internal readonly record struct CliCommandProgressEntry<TPayload> (
    string EventName,
    TPayload Payload)
    where TPayload : notnull;
