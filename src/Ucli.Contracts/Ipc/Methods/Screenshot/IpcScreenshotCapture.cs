namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents screenshot capture metadata observed by Unity. </summary>
/// <param name="Target"> The screenshot target literal. </param>
/// <param name="SizeMode"> The rule used to determine the captured dimensions. </param>
/// <param name="RequestedWidth"> The requested GameView width, or <see langword="null" /> when omitted. </param>
/// <param name="RequestedHeight"> The requested GameView height, or <see langword="null" /> when omitted. </param>
/// <param name="Width"> The captured image width in pixels. </param>
/// <param name="Height"> The captured image height in pixels. </param>
/// <param name="ColorSpace"> The active Unity project color-space literal at capture time. </param>
/// <param name="LifecycleStateAtCapture"> The Unity Editor lifecycle-state literal at capture time. </param>
/// <param name="CompileStateAtCapture"> The Unity Editor compile-state literal at capture time. </param>
/// <param name="DomainReloadGeneration"> The domain-reload generation at capture time. </param>
/// <param name="PlayModeState"> The Play Mode state literal at capture time. </param>
public sealed record IpcScreenshotCapture (
    string Target,
    string SizeMode,
    int? RequestedWidth,
    int? RequestedHeight,
    int Width,
    int Height,
    string ColorSpace,
    string LifecycleStateAtCapture,
    string CompileStateAtCapture,
    long DomainReloadGeneration,
    string PlayModeState);
