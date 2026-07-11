using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Unity project color-space literals used by screenshot capture metadata. </summary>
public enum IpcScreenshotColorSpace
{
    /// <summary> Uses the gamma project color space. </summary>
    [UcliContractLiteral("gamma")]
    Gamma = 0,

    /// <summary> Uses the linear project color space. </summary>
    [UcliContractLiteral("linear")]
    Linear = 1,
}
