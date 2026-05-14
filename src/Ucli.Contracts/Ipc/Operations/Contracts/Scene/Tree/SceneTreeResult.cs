using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene tree operation result.")]
public sealed record SceneTreeResult
{
    [JsonConstructor]
    public SceneTreeResult (
        SceneAssetPath path,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        SceneTreeSourceState sourceState,
        BoundedWindow window)
    {
        Path = path;
        Roots = roots;
        SourceState = sourceState;
        Window = window;
    }

    public SceneTreeResult (
        SceneAssetPath path,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        SceneTreeSourceState sourceState)
        : this(
            path,
            roots,
            sourceState,
            new BoundedWindow(
                limit: null,
                cursor: null,
                nextCursor: null,
                isComplete: true,
                totalCount: null))
    {
    }

    [UcliRequired]
    [UcliDescription("Scene asset path that was described.")]
    public SceneAssetPath Path { get; init; }

    [UcliRequired]
    [UcliDescription("Root GameObjects in the scene.")]
    public IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> Roots { get; init; }

    [UcliRequired]
    [UcliDescription("Source state used to build the scene tree.")]
    public SceneTreeSourceState SourceState { get; init; }

    [UcliRequired]
    [UcliDescription("Bounded result window metadata.")]
    public BoundedWindow Window { get; init; }
}
