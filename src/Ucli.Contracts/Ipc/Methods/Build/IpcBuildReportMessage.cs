namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one normalized BuildReport message. </summary>
/// <param name="Type"> The normalized message type. </param>
/// <param name="Content"> The message content. </param>
public sealed record IpcBuildReportMessage (
    string Type,
    string Content);
