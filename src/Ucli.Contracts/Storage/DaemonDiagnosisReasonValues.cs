namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Defines normalized daemon diagnosis reason values used across CLI and Unity batchmode. </summary>
public static class DaemonDiagnosisReasonValues
{
    /// <summary> Gets the reason value used when daemon shutdown was requested normally. </summary>
    public const string ShutdownRequested = "shutdownRequested";

    /// <summary> Gets the reason value used when daemon startup failed before running state was established. </summary>
    public const string StartupFailed = "startupFailed";

    /// <summary> Gets the reason value used when the IPC server loop terminated unexpectedly. </summary>
    public const string ListenerTerminated = "listenerTerminated";

    /// <summary> Gets the reason value used when daemon bootstrap failed with an unhandled exception. </summary>
    public const string UnhandledException = "unhandledException";

    /// <summary> Gets the reason value used when daemon reaches readiness but fails the stability window. </summary>
    public const string StartupUnstable = "startupUnstable";

    /// <summary> Gets the reason value used when supervisor observes unexpected daemon process termination. </summary>
    public const string UnexpectedExit = "unexpectedExit";

    /// <summary> Gets the reason value used when CLI infers external process termination without persisted diagnosis. </summary>
    public const string ExternalTerminationSuspected = "externalTerminationSuspected";

    /// <summary> Gets the reason value used when a detected GUI Editor process does not register an endpoint before timeout. </summary>
    public const string GuiEndpointNotRegistered = "guiEndpointNotRegistered";

    /// <summary> Gets the reason value used when Unity script compilation blocks GUI daemon bootstrap. </summary>
    public const string UnityScriptCompilationFailed = "unityScriptCompilationFailed";

    /// <summary> Gets the reason value used when Unity package resolution blocks GUI daemon bootstrap. </summary>
    public const string UnityPackageResolutionFailed = "unityPackageResolutionFailed";

    /// <summary> Gets the reason value used when uCLI plugin dependencies are missing during daemon bootstrap. </summary>
    public const string UcliPluginDependencyMissing = "ucliPluginDependencyMissing";

    /// <summary> Gets the reason value used when Unity startup is blocked by a precompiled assembly conflict. </summary>
    public const string PrecompiledAssemblyConflict = "precompiledAssemblyConflict";

    /// <summary> Gets the reason value used when Unity Editor requires user action before GUI daemon bootstrap can continue. </summary>
    public const string EditorUserActionRequired = "unityEditorUserActionRequired";

    /// <summary> Gets the reason value used when Unity Editor exits before GUI daemon bootstrap writes a session. </summary>
    public const string EditorExitedBeforeBootstrap = "unityEditorExitedBeforeBootstrap";
}
