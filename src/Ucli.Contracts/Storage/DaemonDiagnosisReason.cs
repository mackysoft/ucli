using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Identifies the recorded cause of a daemon lifecycle diagnosis. </summary>
public enum DaemonDiagnosisReason
{
    /// <summary> Daemon shutdown was requested normally. </summary>
    [UcliContractLiteral("shutdownRequested")]
    ShutdownRequested = 1,

    /// <summary> Daemon startup failed before a running session was established. </summary>
    [UcliContractLiteral("startupFailed")]
    StartupFailed = 2,

    /// <summary> The IPC server listener terminated unexpectedly. </summary>
    [UcliContractLiteral("listenerTerminated")]
    ListenerTerminated = 3,

    /// <summary> Daemon bootstrap failed with an unhandled exception. </summary>
    [UcliContractLiteral("unhandledException")]
    UnhandledException = 4,

    /// <summary> Daemon readiness did not remain stable for the required observation window. </summary>
    [UcliContractLiteral("startupUnstable")]
    StartupUnstable = 5,

    /// <summary> A supervisor observed an unexpected daemon process exit. </summary>
    [UcliContractLiteral("unexpectedExit")]
    UnexpectedExit = 6,

    /// <summary> CLI inferred that an external actor terminated the daemon process. </summary>
    [UcliContractLiteral("externalTerminationSuspected")]
    ExternalTerminationSuspected = 7,

    /// <summary> A detected GUI Editor did not register its daemon endpoint before the deadline. </summary>
    [UcliContractLiteral("guiEndpointNotRegistered")]
    GuiEndpointNotRegistered = 8,

    /// <summary> A detected GUI Editor could not accept a daemon rebootstrap request. </summary>
    [UcliContractLiteral("guiRebootstrapUnavailable")]
    GuiRebootstrapUnavailable = 9,

    /// <summary> Unity script compilation errors prevented GUI daemon bootstrap. </summary>
    [UcliContractLiteral("unityScriptCompilationFailed")]
    UnityScriptCompilationFailed = 10,

    /// <summary> Unity package resolution errors prevented GUI daemon bootstrap. </summary>
    [UcliContractLiteral("unityPackageResolutionFailed")]
    UnityPackageResolutionFailed = 11,

    /// <summary> A required uCLI plugin dependency was unavailable during daemon bootstrap. </summary>
    [UcliContractLiteral("ucliPluginDependencyMissing")]
    UcliPluginDependencyMissing = 12,

    /// <summary> A precompiled assembly conflict prevented Unity startup. </summary>
    [UcliContractLiteral("precompiledAssemblyConflict")]
    PrecompiledAssemblyConflict = 13,

    /// <summary> Unity Editor requires user action before GUI daemon bootstrap can continue. </summary>
    [UcliContractLiteral("editorUserActionRequired")]
    EditorUserActionRequired = 14,

    /// <summary> Unity Editor exited before GUI daemon bootstrap established a session. </summary>
    [UcliContractLiteral("unityEditorExitedBeforeBootstrap")]
    EditorExitedBeforeBootstrap = 15,
}
