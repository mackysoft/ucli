using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines screenshot capture target literals. </summary>
public enum IpcScreenshotTarget
{
    /// <summary> Captures the main GameView application presentation image. </summary>
    [UcliContractLiteral("game")]
    Game = 1,

    /// <summary> Captures the active SceneView presentation image. </summary>
    [UcliContractLiteral("scene")]
    Scene = 2,
}
