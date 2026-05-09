namespace MackySoft.Ucli.Contracts;

/// <summary> Defines Play Mode mutation error code values. </summary>
public static class PlayModeErrorCodes
{
    /// <summary> Gets the error code emitted when a Play Mode mutation is requested while Play Mode is not active. </summary>
    public static readonly UcliErrorCode PlayModeNotActive = new("PLAYMODE_NOT_ACTIVE");

    /// <summary> Gets the error code emitted when a Play Mode mutation requires a GUI Editor session. </summary>
    public static readonly UcliErrorCode PlayModeRequiresGuiEditor = new("PLAYMODE_REQUIRES_GUI_EDITOR");

    /// <summary> Gets the error code emitted when a Play Mode mutation attempts forbidden persistence. </summary>
    public static readonly UcliErrorCode PlayModePersistenceForbidden = new("PLAYMODE_PERSISTENCE_FORBIDDEN");
}
