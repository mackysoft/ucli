namespace MackySoft.Ucli.Contracts;

/// <summary> Defines media types produced by screenshot commands. </summary>
[VocabularyDefinition]
public enum ScreenshotArtifactMediaType
{
    /// <summary> Identifies a Portable Network Graphics image. </summary>
    [VocabularyText("image/png")]
    Png = 0,
}
