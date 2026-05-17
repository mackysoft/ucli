namespace MackySoft.Ucli.Contracts;

/// <summary> Defines Unity editor lifecycle error code values. </summary>
public static class EditorLifecycleErrorCodes
{
    /// <summary> Gets the error code emitted when Unity editor startup is still in progress. </summary>
    public static readonly UcliCode EditorStarting = new("EDITOR_STARTING");

    /// <summary> Gets the error code emitted when Unity editor is busy with internal work. </summary>
    public static readonly UcliCode EditorBusy = new("EDITOR_BUSY");

    /// <summary> Gets the error code emitted when Unity editor is compiling scripts. </summary>
    public static readonly UcliCode EditorCompiling = new("EDITOR_COMPILING");

    /// <summary> Gets the error code emitted when Unity editor has script compilation failures. </summary>
    public static readonly UcliCode EditorCompileFailed = new("EDITOR_COMPILE_FAILED");

    /// <summary> Gets the error code emitted when Unity editor is reloading the AppDomain. </summary>
    public static readonly UcliCode EditorDomainReloading = new("EDITOR_DOMAIN_RELOADING");

    /// <summary> Gets the error code emitted when Unity editor is recovering after daemon endpoint loss. </summary>
    public static readonly UcliCode EditorRecovering = new("EDITOR_RECOVERING");

    /// <summary> Gets the error code emitted when Unity editor is refreshing or reimporting assets. </summary>
    public static readonly UcliCode EditorReimporting = new("EDITOR_REIMPORTING");

    /// <summary> Gets the error code emitted when Unity editor is in Play Mode. </summary>
    public static readonly UcliCode EditorPlaymode = new("EDITOR_PLAYMODE");

    /// <summary> Gets the error code emitted when a modal dialog blocks Unity editor execution. </summary>
    public static readonly UcliCode EditorModalBlocked = new("EDITOR_MODAL_BLOCKED");

    /// <summary> Gets the error code emitted when Unity editor is in Safe Mode. </summary>
    public static readonly UcliCode EditorSafeMode = new("EDITOR_SAFE_MODE");

    /// <summary> Gets the error code emitted when Unity editor shutdown is in progress. </summary>
    public static readonly UcliCode EditorShuttingDown = new("EDITOR_SHUTTING_DOWN");

    /// <summary> Gets the error code emitted when Unity editor lifecycle cannot be observed. </summary>
    public static readonly UcliCode EditorUnavailable = new("EDITOR_UNAVAILABLE");
}
