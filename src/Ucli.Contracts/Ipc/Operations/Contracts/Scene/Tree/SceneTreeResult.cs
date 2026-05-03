using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene tree operation result.")]
public sealed record SceneTreeResult
{
    [JsonConstructor]
    public SceneTreeResult (
        string path,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots)
    {
        Path = path;
        Roots = roots;
    }

    [UcliRequired]
    [UcliDescription("Scene asset path that was described.")]
    [UcliMinLength(1)]
    public string Path { get; init; }

    [UcliRequired]
    [UcliDescription("Root GameObjects in the scene.")]
    public IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> Roots { get; init; }
}
