namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies where a new GameObject is placed. </summary>
[VocabularyDefinition]
public enum GoCreatePlacementKind
{
    /// <summary> Places a root GameObject in a scene. </summary>
    [VocabularyText("scene")]
    Scene = 1,

    /// <summary> Places a GameObject below an existing parent. </summary>
    [VocabularyText("parent")]
    Parent = 2,
}
