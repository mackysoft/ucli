using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>ping</c> IPC request payload. </summary>
/// <param name="ClientVersion"> The client runtime version string. </param>
/// <param name="FailFast"> Whether lifecycle probing should fail immediately instead of waiting for readiness. </param>
public sealed record IpcPingRequest (
    string ClientVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool FailFast = false);
