using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene query operation arguments.")]
public sealed record SceneQueryArgs
{
    [JsonConstructor]
    public SceneQueryArgs (
        string scene,
        string? pathPrefix,
        string? componentType)
    {
        Scene = scene;
        PathPrefix = pathPrefix;
        ComponentType = componentType;
    }

    [UcliRequired]
    [UcliDescription("Scene asset path to query.")]
    public string Scene { get; init; }

    [UcliDescription("Optional hierarchy path prefix filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PathPrefix { get; init; }

    [UcliDescription("Optional component type identifier filter.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ComponentType { get; init; }
}
