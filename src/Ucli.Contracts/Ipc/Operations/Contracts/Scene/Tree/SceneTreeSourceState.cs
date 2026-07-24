using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Source state used to build a scene tree result.")]
public sealed record SceneTreeSourceState
{
    [JsonConstructor]
    public SceneTreeSourceState (
        SceneTreeSourceStateKind kind,
        bool isDirty)
    {
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Scene tree source kind must be specified.");
        }

        Kind = kind;
        IsDirty = isDirty;
    }

    [UcliRequired]
    [UcliDescription("Source kind used to build the scene tree. Values are temporaryScene, loadedScene, persistedPreview, and readIndex.")]
    [JsonConverter(typeof(VocabularyJsonConverterFactory))]
    public SceneTreeSourceStateKind Kind { get; }

    [UcliRequired]
    [UcliDescription("Whether the source scene contained unsaved Unity editor changes when the tree was read.")]
    public bool IsDirty { get; }
}
