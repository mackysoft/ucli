using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Presentation;

namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Captures Unity Editor presentation surfaces as PNG artifacts. </summary>
internal interface IScreenshotCaptureService
{
    /// <summary> Captures one presentation surface and commits its PNG artifact. </summary>
    ValueTask<ScreenshotCaptureResult> CaptureAsync (
        ScreenshotCaptureInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures through the already fixed Program host. It retains the normal
    /// screenshot artifact commit/cleanup contract without selecting another
    /// daemon session.
    /// </summary>
    ValueTask<ScreenshotCaptureResult> CaptureOnFixedHostAsync (
        ProjectContext context,
        IUnityExecutionHostBinding binding,
        IpcScreenshotTarget target,
        PixelDimensions? requestedDimensions,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);
}
