
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Unity project color-space literals used by screenshot capture metadata. </summary>
[VocabularyDefinition]
public enum IpcScreenshotColorSpace
{
    /// <summary> Uses the gamma project color space. </summary>
    [VocabularyText("gamma")]
    Gamma = 1,

    /// <summary> Uses the linear project color space. </summary>
    [VocabularyText("linear")]
    Linear = 2,
}
