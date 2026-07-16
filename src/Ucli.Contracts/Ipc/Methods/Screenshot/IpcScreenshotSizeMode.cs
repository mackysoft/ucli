using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines screenshot capture size-mode literals. </summary>
public enum IpcScreenshotSizeMode
{
    /// <summary> Captures the current target-surface size. </summary>
    [UcliContractLiteral("currentSurface")]
    CurrentSurface = 1,

    /// <summary> Captures an explicitly requested GameView resolution. </summary>
    [UcliContractLiteral("requestedResolution")]
    RequestedResolution = 2,
}
