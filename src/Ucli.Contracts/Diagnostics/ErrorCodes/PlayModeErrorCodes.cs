namespace MackySoft.Ucli.Contracts;

/// <summary> Defines Play Mode mutation and lifecycle control error code values. </summary>
public static class PlayModeErrorCodes
{
    /// <summary> Gets the error code emitted when a Play Mode mutation is requested while Play Mode is not active. </summary>
    public static readonly UcliCode PlayModeNotActive = new("PLAYMODE_NOT_ACTIVE");

    /// <summary> Gets the error code emitted when a Play Mode mutation requires a GUI Editor session. </summary>
    public static readonly UcliCode PlayModeRequiresGuiEditor = new("PLAYMODE_REQUIRES_GUI_EDITOR");

    /// <summary> Gets the error code emitted when a Play Mode mutation attempts forbidden persistence. </summary>
    public static readonly UcliCode PlayModePersistenceForbidden = new("PLAYMODE_PERSISTENCE_FORBIDDEN");

    /// <summary> Gets the error code emitted when Play Mode control cannot find a registered GUI daemon session. </summary>
    public static readonly UcliCode PlayModeSessionNotAvailable = new("PLAYMODE_SESSION_NOT_AVAILABLE");

    /// <summary> Gets the error code emitted when Play Mode control does not reach the requested state before its transition timeout. </summary>
    public static readonly UcliCode PlayModeTransitionTimeout = new("PLAYMODE_TRANSITION_TIMEOUT");

    /// <summary> Gets the error code emitted when Play Mode control is blocked by a non-timeout Editor state. </summary>
    public static readonly UcliCode PlayModeTransitionBlocked = new("PLAYMODE_TRANSITION_BLOCKED");

    /// <summary> Gets the error code emitted when Play Mode control is requested while another Play Mode transition is already in progress. </summary>
    public static readonly UcliCode PlayModeAlreadyChanging = new("PLAYMODE_ALREADY_CHANGING");

    /// <summary> Gets the error code emitted when Unity rejects a Play Mode enter request. </summary>
    public static readonly UcliCode PlayModeEnterRejected = new("PLAYMODE_ENTER_REJECTED");

    /// <summary> Gets the error code emitted when Unity rejects a Play Mode exit request. </summary>
    public static readonly UcliCode PlayModeExitRejected = new("PLAYMODE_EXIT_REJECTED");

    /// <summary> Gets the error code emitted when Play Mode state cannot be classified. </summary>
    public static readonly UcliCode PlayModeStateUnknown = new("PLAYMODE_STATE_UNKNOWN");
}
