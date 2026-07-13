using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents screenshot capture metadata observed by Unity. </summary>
public sealed record IpcScreenshotCapture
{
    /// <summary> Initializes screenshot capture metadata observed by Unity. </summary>
    [JsonConstructor]
    public IpcScreenshotCapture (
        IpcScreenshotTarget target,
        IpcScreenshotSizeMode sizeMode,
        int? requestedWidth,
        int? requestedHeight,
        int width,
        int height,
        IpcScreenshotColorSpace colorSpace,
        UnityEditorStateSnapshot state)
    {
        Target = target;
        SizeMode = sizeMode;
        RequestedWidth = requestedWidth;
        RequestedHeight = requestedHeight;
        Width = width;
        Height = height;
        ColorSpace = colorSpace;
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <summary> Gets the screenshot target. </summary>
    public IpcScreenshotTarget Target { get; }

    /// <summary> Gets the rule used to determine the captured dimensions. </summary>
    public IpcScreenshotSizeMode SizeMode { get; }

    /// <summary> Gets the requested GameView width, or <see langword="null" /> when omitted. </summary>
    public int? RequestedWidth { get; }

    /// <summary> Gets the requested GameView height, or <see langword="null" /> when omitted. </summary>
    public int? RequestedHeight { get; }

    /// <summary> Gets the captured image width in pixels. </summary>
    public int Width { get; }

    /// <summary> Gets the captured image height in pixels. </summary>
    public int Height { get; }

    /// <summary> Gets the active Unity project color space at capture time. </summary>
    public IpcScreenshotColorSpace ColorSpace { get; }

    /// <summary> Gets the comparable Unity Editor state at capture time. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityEditorStateSnapshot State { get; private init; }
}
