using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents internally consistent screenshot capture metadata observed by Unity. </summary>
public sealed record IpcScreenshotCapture
{
    /// <summary> Initializes screenshot capture metadata observed at the successful pixel-readback boundary. </summary>
    /// <exception cref="ArgumentException"> Thrown when the target, size mode, requested size, captured size, or capture state are inconsistent. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="State" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a required contract literal or captured dimension is invalid. </exception>
    [JsonConstructor]
    public IpcScreenshotCapture (
        IpcScreenshotTarget Target,
        IpcScreenshotSizeMode SizeMode,
        PixelDimensions? RequestedDimensions,
        PixelDimensions Dimensions,
        UnityProjectColorSpace ProjectColorSpace,
        UnityEditorStateSnapshot State)
    {
        if (!TextVocabulary.IsDefined(Target))
        {
            throw new ArgumentOutOfRangeException(nameof(Target), Target, "Screenshot target must be specified.");
        }

        if (!TextVocabulary.IsDefined(SizeMode))
        {
            throw new ArgumentOutOfRangeException(nameof(SizeMode), SizeMode, "Screenshot size mode must be specified.");
        }

        if (!TextVocabulary.IsDefined(ProjectColorSpace))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ProjectColorSpace),
                ProjectColorSpace,
                "Unity project color space must be specified.");
        }

        var state = State ?? throw new ArgumentNullException(nameof(State));
        if (!IsSuccessfulCaptureState(state))
        {
            throw new ArgumentException(
                "Screenshot capture state must represent a stable supported Editor presentation state.",
                nameof(State));
        }

        var dimensions = Dimensions ?? throw new ArgumentNullException(nameof(Dimensions));
        if (!IpcScreenshotCaptureLimits.TryCalculateRgba8Layout(
            dimensions.Width,
            dimensions.Height,
            out _,
            out _))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Dimensions),
                "Captured screenshot dimensions exceed the supported normalized RGBA8 layout.");
        }

        switch (SizeMode)
        {
            case IpcScreenshotSizeMode.CurrentSurface when RequestedDimensions is not null:
                throw new ArgumentException(
                    "Current-surface capture metadata must not contain requested dimensions.",
                    nameof(RequestedDimensions));
            case IpcScreenshotSizeMode.CurrentSurface:
                break;
            case IpcScreenshotSizeMode.RequestedResolution when RequestedDimensions is null:
                throw new ArgumentException(
                    "Requested-resolution capture metadata must contain requested dimensions.",
                    nameof(RequestedDimensions));
            case IpcScreenshotSizeMode.RequestedResolution:
                if (Target != IpcScreenshotTarget.Game)
                {
                    throw new ArgumentException(
                        "Requested-resolution capture metadata is valid only for the game screenshot target.",
                        nameof(Target));
                }

                if (dimensions != RequestedDimensions)
                {
                    throw new ArgumentException(
                        "Captured dimensions must match requested-resolution dimensions.",
                        nameof(Dimensions));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(SizeMode),
                    SizeMode,
                    "Screenshot size mode is not supported by the capture metadata contract.");
        }

        this.Target = Target;
        this.SizeMode = SizeMode;
        this.RequestedDimensions = RequestedDimensions;
        this.Dimensions = dimensions;
        this.ProjectColorSpace = ProjectColorSpace;
        this.State = state;
    }

    /// <summary> Determines whether an Editor state can produce successful screenshot metadata. </summary>
    /// <param name="state"> The Editor state observed at the pixel-readback boundary. </param>
    /// <returns>
    /// <see langword="true" /> when the state represents stable Edit Mode or stable Play Mode;
    /// otherwise, <see langword="false" />.
    /// </returns>
    internal static bool IsSuccessfulCaptureState (UnityEditorStateSnapshot state)
    {
        if (state.EditorMode != UnityEditorMode.Gui
            || state.CompileState != UnityEditorCompileState.Ready)
        {
            return false;
        }

        var playMode = state.PlayMode;
        if (playMode.Transition != UnityEditorPlayModeTransition.None)
        {
            return false;
        }

        if (state.LifecycleState == UnityEditorLifecycleState.Ready
            && playMode.State == UnityEditorPlayModeState.Stopped
            && !playMode.IsPlaying
            && !playMode.IsPlayingOrWillChangePlaymode)
        {
            return true;
        }

        return state.LifecycleState == UnityEditorLifecycleState.PlayMode
            && playMode.State == UnityEditorPlayModeState.Playing
            && playMode.IsPlaying
            && playMode.IsPlayingOrWillChangePlaymode;
    }

    /// <summary> Gets the screenshot target. </summary>
    public IpcScreenshotTarget Target { get; }

    /// <summary> Gets the rule used to determine the captured dimensions. </summary>
    public IpcScreenshotSizeMode SizeMode { get; }

    /// <summary> Gets the requested GameView dimensions, or <see langword="null" /> when omitted. </summary>
    public PixelDimensions? RequestedDimensions { get; }

    /// <summary> Gets the captured image dimensions. </summary>
    public PixelDimensions Dimensions { get; }

    /// <summary> Gets the active Unity project color space at capture time. </summary>
    public UnityProjectColorSpace ProjectColorSpace { get; }

    /// <summary> Gets the comparable Unity Editor state at capture time. </summary>
    public UnityEditorStateSnapshot State { get; }
}
