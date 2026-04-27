using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one <c>index.assets.read</c> IPC request payload. </summary>
/// <param name="FailFast"> Whether readiness gating should fail immediately instead of waiting. </param>
public sealed record IpcIndexAssetsReadRequest (
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool FailFast = false);
