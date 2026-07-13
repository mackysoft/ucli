using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines normalized raw screenshot row-order literals. </summary>
public enum IpcScreenshotRowOrder
{
    /// <summary> Orders rows from the top of the image to the bottom. </summary>
    [UcliContractLiteral("topDown")]
    TopDown = 0,
}
