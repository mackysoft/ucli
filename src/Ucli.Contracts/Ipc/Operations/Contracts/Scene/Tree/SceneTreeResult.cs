using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Scene tree operation result.")]
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

    [JsonInclude]
    [JsonRequired]
    [Description("Scene asset path that was described.")]
    public UnityScenePath Path { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Root GameObjects in the scene.")]
    public IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> Roots { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Source state used to build the scene tree.")]
    public SceneTreeSourceState SourceState { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Bounded result window metadata.")]
    public BoundedWindow Window { get; private init; }
}
