using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines asset kinds that can be inspected for missing script slots. </summary>
[VocabularyDefinition]
public enum MissingScriptsAssetKind
{
    /// <summary> A saved Unity scene asset. </summary>
    [VocabularyText("scene")]
    Scene = 1,

    /// <summary> A saved Unity prefab asset. </summary>
    [VocabularyText("prefab")]
    Prefab = 2,
}
