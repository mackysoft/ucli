using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene tree operation result.")]
public sealed record SceneTreeResult
{
    [JsonConstructor]
    public SceneTreeResult (
        UnityScenePath path,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        SceneTreeSourceState sourceState,
        BoundedWindow window)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Roots = ContractArgumentGuard.RequireItems(roots, nameof(roots));
        SourceState = sourceState ?? throw new ArgumentNullException(nameof(sourceState));
        Window = window ?? throw new ArgumentNullException(nameof(window));
    }

    [UcliRequired]
    [UcliDescription("Scene asset path that was described.")]
    public UnityScenePath Path { get; }

    [UcliRequired]
    [UcliDescription("Root GameObjects in the scene.")]
    public IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> Roots { get; }

    [UcliRequired]
    [UcliDescription("Source state used to build the scene tree.")]
    public SceneTreeSourceState SourceState { get; }

    [UcliRequired]
    [UcliDescription("Bounded result window metadata.")]
    public BoundedWindow Window { get; }
}
