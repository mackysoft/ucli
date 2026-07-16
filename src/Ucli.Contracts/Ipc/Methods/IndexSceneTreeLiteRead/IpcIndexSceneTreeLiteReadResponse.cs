using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one <c>index.scene-tree-lite.read</c> IPC response payload. </summary>
public sealed record IpcIndexSceneTreeLiteReadResponse
{
    [JsonConstructor]
    public IpcIndexSceneTreeLiteReadResponse (
        DateTimeOffset GeneratedAtUtc,
        UnityScenePath ScenePath,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? Roots,
        SceneTreeSourceState SourceState)
    {
        if (GeneratedAtUtc == default || GeneratedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Snapshot generation timestamp must be a non-default UTC value.", nameof(GeneratedAtUtc));
        }

        this.GeneratedAtUtc = GeneratedAtUtc;
        this.ScenePath = ScenePath ?? throw new ArgumentNullException(nameof(ScenePath));
        this.Roots = ContractArgumentGuard.RequireItems(Roots, nameof(Roots));
        this.SourceState = SourceState ?? throw new ArgumentNullException(nameof(SourceState));
    }

    /// <summary> Gets the server-side snapshot generation timestamp. </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary> Gets the project-relative scene path represented by this snapshot. </summary>
    public UnityScenePath ScenePath { get; }

    /// <summary> Gets the root scene-tree-lite nodes. </summary>
    public IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> Roots { get; }

    /// <summary> Gets the source state used to build this snapshot. </summary>
    public SceneTreeSourceState SourceState { get; }
}
