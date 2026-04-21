namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one <c>index.scene-tree-lite.read</c> IPC request payload. </summary>
/// <param name="ScenePath"> The project-relative scene path to snapshot. </param>
public sealed record IpcIndexSceneTreeLiteReadRequest (
    string ScenePath);