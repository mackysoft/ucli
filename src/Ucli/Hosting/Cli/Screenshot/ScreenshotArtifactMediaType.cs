namespace MackySoft.Ucli.Hosting.Cli.Screenshot;

/// <summary> Identifies the media type emitted for a committed screenshot artifact. </summary>
[VocabularyDefinition]
internal enum ScreenshotArtifactMediaType
{
    /// <summary> A Portable Network Graphics image. </summary>
    [VocabularyText("image/png")]
    Png = 0,
}
