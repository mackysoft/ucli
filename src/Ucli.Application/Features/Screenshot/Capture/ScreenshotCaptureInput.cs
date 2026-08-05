using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Presentation;

namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Represents inputs for one screenshot capture workflow. </summary>
/// <param name="Target"> The presentation surface to capture. </param>
/// <param name="ProjectPath"> The optional target Unity project path. </param>
/// <param name="RequestedDimensions"> The requested GameView render dimensions, or <see langword="null" /> for the current surface. </param>
/// <param name="TimeoutMilliseconds"> The optional timeout override in milliseconds. </param>
internal sealed record ScreenshotCaptureInput (
    IpcScreenshotTarget Target,
    AbsolutePath? ProjectPath,
    PixelDimensions? RequestedDimensions,
    int? TimeoutMilliseconds);
