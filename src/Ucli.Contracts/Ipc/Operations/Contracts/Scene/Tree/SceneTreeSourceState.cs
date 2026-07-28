using System.Text.Json.Serialization;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

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

    [JsonInclude]
    [JsonRequired]
    [Description("Source kind used to build the scene tree.")]
    [JsonConverter(typeof(VocabularyJsonConverterFactory))]
    public SceneTreeSourceStateKind Kind { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Whether the source scene contained unsaved Unity editor changes when the tree was read.")]
    public bool IsDirty { get; private init; }
}
