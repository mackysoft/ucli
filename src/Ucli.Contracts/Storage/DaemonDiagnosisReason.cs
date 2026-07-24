
namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Identifies the recorded cause of a daemon lifecycle diagnosis. </summary>
[VocabularyDefinition]
public enum DaemonDiagnosisReason
{
    /// <summary> Daemon shutdown was requested normally. </summary>
    [VocabularyText("shutdownRequested")]
    ShutdownRequested = 1,

    /// <summary> Daemon startup failed before a running session was established. </summary>
    [VocabularyText("startupFailed")]
    StartupFailed = 2,

    /// <summary> The IPC server listener terminated unexpectedly. </summary>
    [VocabularyText("listenerTerminated")]
    ListenerTerminated = 3,

    /// <summary> Daemon bootstrap failed with an unhandled exception. </summary>
    [VocabularyText("unhandledException")]
    UnhandledException = 4,

    /// <summary> Daemon readiness did not remain stable for the required observation window. </summary>
    [VocabularyText("startupUnstable")]
    StartupUnstable = 5,

    /// <summary> A supervisor observed an unexpected daemon process exit. </summary>
    [VocabularyText("unexpectedExit")]
    UnexpectedExit = 6,

    /// <summary> CLI inferred that an external actor terminated the daemon process. </summary>
    [VocabularyText("externalTerminationSuspected")]
    ExternalTerminationSuspected = 7,

    /// <summary> A detected GUI Editor did not register its daemon endpoint before the deadline. </summary>
    [VocabularyText("guiEndpointNotRegistered")]
    GuiEndpointNotRegistered = 8,

    /// <summary> A detected GUI Editor could not accept a daemon rebootstrap request. </summary>
    [VocabularyText("guiRebootstrapUnavailable")]
    GuiRebootstrapUnavailable = 9,

    /// <summary> Unity script compilation errors prevented GUI daemon bootstrap. </summary>
    [VocabularyText("unityScriptCompilationFailed")]
    UnityScriptCompilationFailed = 10,

    /// <summary> Unity package resolution errors prevented GUI daemon bootstrap. </summary>
    [VocabularyText("unityPackageResolutionFailed")]
    UnityPackageResolutionFailed = 11,

    /// <summary> A required uCLI plugin dependency was unavailable during daemon bootstrap. </summary>
    [VocabularyText("ucliPluginDependencyMissing")]
    UcliPluginDependencyMissing = 12,

    /// <summary> A precompiled assembly conflict prevented Unity startup. </summary>
    [VocabularyText("precompiledAssemblyConflict")]
    PrecompiledAssemblyConflict = 13,

    /// <summary> Unity Editor requires user action before GUI daemon bootstrap can continue. </summary>
    [VocabularyText("editorUserActionRequired")]
    EditorUserActionRequired = 14,

    /// <summary> Unity Editor exited before GUI daemon bootstrap established a session. </summary>
    [VocabularyText("unityEditorExitedBeforeBootstrap")]
    EditorExitedBeforeBootstrap = 15,
}
