using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one <c>index.scene-tree-lite.read</c> IPC request payload. </summary>
public sealed record IpcIndexSceneTreeLiteReadRequest
{
    /// <summary> Initializes a validated scene-tree-lite read request. </summary>
    /// <param name="ScenePath"> The validated project-relative scene path to read. </param>
    /// <param name="FailFast"> Whether readiness gating fails immediately. </param>
    /// <param name="LoadedSceneOnly"> Whether reading a closed scene is rejected. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="ScenePath" /> is <see langword="null" />. </exception>
    [JsonConstructor]
    public IpcIndexSceneTreeLiteReadRequest (
        UnityScenePath ScenePath,
        bool FailFast,
        bool LoadedSceneOnly)
    {
        this.ScenePath = ScenePath ?? throw new ArgumentNullException(nameof(ScenePath));
        this.FailFast = FailFast;
        this.LoadedSceneOnly = LoadedSceneOnly;
    }

    /// <summary> Gets the project-relative scene path to snapshot. </summary>
    public UnityScenePath ScenePath { get; }

    /// <summary> Gets whether readiness gating should fail immediately instead of waiting. </summary>
    public bool FailFast { get; }

    /// <summary> Gets whether the read should fail instead of opening a persisted preview scene when the scene is not loaded. </summary>
    public bool LoadedSceneOnly { get; }
}
