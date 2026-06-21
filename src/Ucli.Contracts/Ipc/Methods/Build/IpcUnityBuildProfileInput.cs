using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the Unity Build Profile input selected by path. </summary>
/// <param name="Path"> The canonical project-relative asset path. </param>
/// <param name="Digest"> The lowercase SHA-256 digest of the resolved asset file when available. </param>
/// <param name="ApplyAudit"> The profile application audit when available. </param>
public sealed record IpcUnityBuildProfileInput (
    string Path,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Digest = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IpcUnityBuildProfileApplyAudit? ApplyAudit = null);
