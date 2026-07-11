namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Represents normalized inputs for one screenshot capture. </summary>
/// <param name="Target"> The presentation surface to capture. </param>
/// <param name="ProjectPath"> The optional target Unity project path. </param>
/// <param name="RequestedWidth"> The requested GameView render width, or <see langword="null" /> for the current surface. </param>
/// <param name="RequestedHeight"> The requested GameView render height, or <see langword="null" /> for the current surface. </param>
/// <param name="TimeoutMilliseconds"> The optional timeout override in milliseconds. </param>
internal sealed record ScreenshotCaptureInput (
    ScreenshotCaptureTarget Target,
    string? ProjectPath,
    int? RequestedWidth,
    int? RequestedHeight,
    int? TimeoutMilliseconds);
