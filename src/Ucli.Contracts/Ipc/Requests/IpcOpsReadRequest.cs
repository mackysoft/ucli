using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one <c>ops.read</c> IPC request payload. </summary>
/// <param name="FailFast"> Whether execution should fail immediately instead of waiting for lifecycle readiness when readiness gating is required. </param>
/// <param name="RequireReadinessGate"> Whether the Unity-side readiness gate should be applied to this catalog read. </param>
public sealed record IpcOpsReadRequest (
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool FailFast = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool RequireReadinessGate = false);