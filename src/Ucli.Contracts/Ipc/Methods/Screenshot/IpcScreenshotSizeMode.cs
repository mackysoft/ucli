
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines screenshot capture size-mode literals. </summary>
[VocabularyDefinition]
public enum IpcScreenshotSizeMode
{
    /// <summary> Captures the current target-surface size. </summary>
    [VocabularyText("currentSurface")]
    CurrentSurface = 1,

    /// <summary> Captures an explicitly requested GameView resolution. </summary>
    [VocabularyText("requestedResolution")]
    RequestedResolution = 2,
}
