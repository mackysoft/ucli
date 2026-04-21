using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one <c>index.scene-tree-lite.read</c> IPC response payload. </summary>
/// <param name="GeneratedAtUtc"> The server-side snapshot generation timestamp. </param>
/// <param name="ScenePath"> The project-relative scene path represented by this snapshot. </param>
/// <param name="Roots"> The root scene-tree-lite nodes. </param>
public sealed record IpcIndexSceneTreeLiteReadResponse (
    DateTimeOffset GeneratedAtUtc,
    string ScenePath,
    IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? Roots);