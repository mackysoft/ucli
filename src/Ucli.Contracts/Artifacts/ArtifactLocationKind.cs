namespace MackySoft.Ucli.Contracts;

/// <summary> Identifies which finalized artifact locators are present. </summary>
[VocabularyDefinition]
public enum ArtifactLocationKind
{
    /// <summary> The artifact is located by a repository-relative path. </summary>
    [VocabularyText("path")]
    Path = 0,

    /// <summary> The artifact is located by an absolute URI. </summary>
    [VocabularyText("uri")]
    Uri,

    /// <summary> The same artifact bytes are located by both a path and a URI. </summary>
    [VocabularyText("pathAndUri")]
    PathAndUri,
}
