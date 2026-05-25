using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one IPC streaming frame returned before or at terminal response completion. </summary>
/// <param name="ProtocolVersion"> The protocol version used by the frame. </param>
/// <param name="RequestId"> The request identifier copied from the incoming request. </param>
/// <param name="Kind"> The stream frame kind. </param>
/// <param name="Event"> The progress event name when <paramref name="Kind" /> is <see cref="IpcStreamFrameKinds.Progress" />. </param>
/// <param name="Payload"> The frame payload. </param>
/// <param name="Response"> The terminal response when <paramref name="Kind" /> is <see cref="IpcStreamFrameKinds.Terminal" />. </param>
public sealed record IpcStreamFrame (
    int ProtocolVersion,
    string RequestId,
    string Kind,
    string? Event,
    JsonElement Payload,
    IpcResponse? Response);
