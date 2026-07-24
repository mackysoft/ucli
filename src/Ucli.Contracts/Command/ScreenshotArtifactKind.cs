
namespace MackySoft.Ucli.Contracts;

/// <summary> Defines semantic artifact kinds produced by screenshot commands. </summary>
[VocabularyDefinition]
public enum ScreenshotArtifactKind
{
    /// <summary> Identifies a committed screenshot image artifact. </summary>
    [VocabularyText("screenshot")]
    Screenshot = 0,
}
