
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines screenshot capture target literals. </summary>
[VocabularyDefinition]
public enum IpcScreenshotTarget
{
    /// <summary> Captures the main GameView application presentation image. </summary>
    [VocabularyText("game")]
    Game = 1,

    /// <summary> Captures the active SceneView presentation image. </summary>
    [VocabularyText("scene")]
    Scene = 2,
}
