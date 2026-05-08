using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Source state used to build a scene tree result.")]
public sealed record SceneTreeSourceState
{
    [JsonConstructor]
    public SceneTreeSourceState (
        SceneTreeSourceStateKind kind,
        bool isDirty)
    {
        Kind = kind;
        IsDirty = isDirty;
    }

    [UcliRequired]
    [UcliDescription("Source kind used to build the scene tree. Values are temporaryScene, loadedScene, persistedPreview, and readIndex.")]
    [JsonConverter(typeof(SceneTreeSourceStateKindJsonConverter))]
    public SceneTreeSourceStateKind Kind { get; init; }

    [UcliRequired]
    [UcliDescription("Whether the source scene contained unsaved Unity editor changes when the tree was read.")]
    public bool IsDirty { get; init; }
}
