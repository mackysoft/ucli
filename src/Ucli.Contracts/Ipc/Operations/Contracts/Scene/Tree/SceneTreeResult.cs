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
        SceneTreeSourceState sourceState)
    {
        Path = path;
        Roots = roots;
        SourceState = sourceState;
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
}
