
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines normalized raw screenshot pixel-format literals. </summary>
[VocabularyDefinition]
public enum IpcScreenshotPixelFormat
{
    /// <summary> Four-channel, 8-bit-per-channel, sRGB-encoded RGBA pixels. </summary>
    [VocabularyText("rgba8Srgb")]
    Rgba8Srgb = 1,
}
