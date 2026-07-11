using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Defines the Unity Editor presentation surface captured by a screenshot command. </summary>
internal enum ScreenshotCaptureTarget
{
    /// <summary> Captures the main GameView application presentation image. </summary>
    [UcliContractLiteral("game")]
    Game = 0,

    /// <summary> Captures the active SceneView scene presentation image. </summary>
    [UcliContractLiteral("scene")]
    Scene = 1,
}
