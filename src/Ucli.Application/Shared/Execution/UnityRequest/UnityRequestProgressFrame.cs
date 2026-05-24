using System.Text.Json;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents one method-owned progress frame received from Unity IPC. </summary>
/// <param name="Event"> The method-specific progress event name. </param>
/// <param name="Payload"> The method-specific progress payload. </param>
internal sealed record UnityRequestProgressFrame (
    string Event,
    JsonElement Payload);
