using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines normalized raw screenshot pixel-format literals. </summary>
public enum IpcScreenshotPixelFormat
{
    /// <summary> Four-channel, 8-bit-per-channel, sRGB-encoded RGBA pixels. </summary>
    [UcliContractLiteral("rgba8Srgb")]
    Rgba8Srgb = 0,
}
