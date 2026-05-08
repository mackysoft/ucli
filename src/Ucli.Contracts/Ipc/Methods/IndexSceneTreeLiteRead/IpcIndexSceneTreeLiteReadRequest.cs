namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one <c>index.scene-tree-lite.read</c> IPC request payload. </summary>
/// <param name="ScenePath"> The project-relative scene path to snapshot. </param>
/// <param name="FailFast"> Whether readiness gating should fail immediately instead of waiting. </param>
/// <param name="LoadedSceneOnly"> Whether the read should fail instead of opening a persisted preview scene when the scene is not loaded. </param>
public sealed record IpcIndexSceneTreeLiteReadRequest (
    string ScenePath,
    bool FailFast = false,
    bool LoadedSceneOnly = false);
