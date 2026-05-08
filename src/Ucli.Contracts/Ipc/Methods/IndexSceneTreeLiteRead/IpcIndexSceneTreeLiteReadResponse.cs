using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one <c>index.scene-tree-lite.read</c> IPC response payload. </summary>
public sealed record IpcIndexSceneTreeLiteReadResponse
{
    [JsonConstructor]
    public IpcIndexSceneTreeLiteReadResponse (
        DateTimeOffset GeneratedAtUtc,
        string ScenePath,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? Roots,
        SceneTreeSourceState SourceState)
    {
        this.GeneratedAtUtc = GeneratedAtUtc;
        this.ScenePath = ScenePath;
        this.Roots = Roots;
        this.SourceState = SourceState;
    }

    /// <summary> Initializes a response that represents persisted-preview source state. </summary>
    public IpcIndexSceneTreeLiteReadResponse (
        DateTimeOffset GeneratedAtUtc,
        string ScenePath,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? Roots)
        : this(
            GeneratedAtUtc,
            ScenePath,
            Roots,
            new SceneTreeSourceState(SceneTreeSourceStateKind.PersistedPreview, isDirty: false))
    {
    }

    /// <summary> Gets the server-side snapshot generation timestamp. </summary>
    public DateTimeOffset GeneratedAtUtc { get; init; }

    /// <summary> Gets the project-relative scene path represented by this snapshot. </summary>
    public string ScenePath { get; init; }

    /// <summary> Gets the root scene-tree-lite nodes. </summary>
    public IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? Roots { get; init; }

    /// <summary> Gets the source state used to build this snapshot. </summary>
    public SceneTreeSourceState SourceState { get; init; }
}
