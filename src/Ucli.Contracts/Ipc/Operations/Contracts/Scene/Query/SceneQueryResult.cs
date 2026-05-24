using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene query operation result.")]
public sealed record SceneQueryResult
{
    [JsonConstructor]
    public SceneQueryResult (
        SceneAssetPath scene,
        IReadOnlyList<SceneQueryMatch> matches)
    {
        Scene = scene;
        Matches = matches;
    }

    [UcliRequired]
    [UcliDescription("Scene asset path that was queried.")]
    public SceneAssetPath Scene { get; init; }

    [UcliRequired]
    [UcliDescription("Matched scene objects or components.")]
    public IReadOnlyList<SceneQueryMatch> Matches { get; init; }
}
