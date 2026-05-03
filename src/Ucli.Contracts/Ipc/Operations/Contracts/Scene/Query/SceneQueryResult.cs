using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene query operation result.")]
public sealed record SceneQueryResult
{
    [JsonConstructor]
    public SceneQueryResult (
        string scene,
        IReadOnlyList<SceneQueryMatch> matches)
    {
        Scene = scene;
        Matches = matches;
    }

    [UcliRequired]
    [UcliDescription("Scene asset path that was queried.")]
    [UcliMinLength(1)]
    public string Scene { get; init; }

    [UcliRequired]
    [UcliDescription("Matched scene objects or components.")]
    public IReadOnlyList<SceneQueryMatch> Matches { get; init; }
}
