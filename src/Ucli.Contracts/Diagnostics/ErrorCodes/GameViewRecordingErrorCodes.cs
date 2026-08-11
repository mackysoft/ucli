namespace MackySoft.Ucli.Contracts;

/// <summary>Defines machine-readable error codes owned by GameView recording.</summary>
public static class GameViewRecordingErrorCodes
{
    public static readonly UcliCode Unavailable = new("GAME_VIEW_RECORDING_UNAVAILABLE");

    public static readonly UcliCode RecorderUnsupported = new("GAME_VIEW_RECORDING_RECORDER_UNSUPPORTED");

    public static readonly UcliCode AdapterFaulted = new("GAME_VIEW_RECORDING_ADAPTER_FAULTED");

    public static readonly UcliCode RequiresGuiSession = new("GAME_VIEW_RECORDING_REQUIRES_GUI_SESSION");

    public static readonly UcliCode RequiresPlayMode = new("GAME_VIEW_RECORDING_REQUIRES_PLAY_MODE");

    public static readonly UcliCode PlayModeTransitioning = new("GAME_VIEW_RECORDING_PLAY_MODE_TRANSITIONING");

    public static readonly UcliCode EditorPaused = new("GAME_VIEW_RECORDING_EDITOR_PAUSED");

    public static readonly UcliCode RequestedSizeUnsupported = new("GAME_VIEW_RECORDING_REQUESTED_SIZE_UNSUPPORTED");

    public static readonly UcliCode EncoderUnsupported = new("GAME_VIEW_RECORDING_ENCODER_UNSUPPORTED");

    public static readonly UcliCode IdConflict = new("GAME_VIEW_RECORDING_ID_CONFLICT");

    public static readonly UcliCode Conflict = new("GAME_VIEW_RECORDING_CONFLICT");

    public static readonly UcliCode NotFound = new("GAME_VIEW_RECORDING_NOT_FOUND");

    public static readonly UcliCode MonitoringTimeout = new("GAME_VIEW_RECORDING_MONITORING_TIMEOUT");

    public static readonly UcliCode BindingMismatch = new("GAME_VIEW_RECORDING_BINDING_MISMATCH");

    public static readonly UcliCode DispatchDeadlineExceeded = new("GAME_VIEW_RECORDING_DISPATCH_DEADLINE_EXCEEDED");

    public static readonly UcliCode FinalizationFailed = new("GAME_VIEW_RECORDING_FINALIZATION_FAILED");

    public static readonly UcliCode CleanupFailed = new("GAME_VIEW_RECORDING_CLEANUP_FAILED");

    public static readonly UcliCode Interrupted = new("GAME_VIEW_RECORDING_INTERRUPTED");

    public static IReadOnlyList<UcliCode> All { get; } =
    [
        Unavailable,
        RecorderUnsupported,
        AdapterFaulted,
        RequiresGuiSession,
        RequiresPlayMode,
        PlayModeTransitioning,
        EditorPaused,
        RequestedSizeUnsupported,
        EncoderUnsupported,
        IdConflict,
        Conflict,
        NotFound,
        MonitoringTimeout,
        BindingMismatch,
        DispatchDeadlineExceeded,
        FinalizationFailed,
        CleanupFailed,
        Interrupted,
    ];
}
