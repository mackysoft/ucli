namespace MackySoft.Ucli.Contracts;

/// <summary> Defines daemon lifecycle machine-readable error code values. </summary>
public static class DaemonErrorCodes
{
    /// <summary> Gets the error code emitted when a requested Editor mode conflicts with an existing daemon session. </summary>
    public static readonly UcliErrorCode DaemonEditorModeMismatch = new("DAEMON_EDITOR_MODE_MISMATCH");

    /// <summary> Gets the error code emitted when daemon startup is blocked by a known Unity Editor startup condition. </summary>
    public static readonly UcliErrorCode DaemonStartupBlocked = new("DAEMON_STARTUP_BLOCKED");

    /// <summary> Gets the error code emitted when Unity exits before daemon endpoint registration completes. </summary>
    public static readonly UcliErrorCode DaemonStartProcessExited = new("DAEMON_START_PROCESS_EXITED");

    /// <summary> Gets the error code emitted when the daemon endpoint is not registered before the startup budget expires. </summary>
    public static readonly UcliErrorCode DaemonEndpointNotRegistered = new("DAEMON_ENDPOINT_NOT_REGISTERED");

    /// <summary> Gets the error code emitted when Unity startup is blocked by script compile errors. </summary>
    public static readonly UcliErrorCode EditorCompileErrors = new("EDITOR_COMPILE_ERRORS");

    /// <summary> Gets the error code emitted when Unity package resolution blocks startup. </summary>
    public static readonly UcliErrorCode PackageResolutionFailed = new("PACKAGE_RESOLUTION_FAILED");

    /// <summary> Gets the error code emitted when a uCLI plugin dependency is missing during startup. </summary>
    public static readonly UcliErrorCode UcliPluginDependencyMissing = new("UCLI_PLUGIN_DEPENDENCY_MISSING");

    /// <summary> Gets the error code emitted when the uCLI plugin fails to compile during startup. </summary>
    public static readonly UcliErrorCode UcliPluginCompileFailed = new("UCLI_PLUGIN_COMPILE_FAILED");

    /// <summary> Gets the error code emitted when precompiled assemblies conflict during Unity startup. </summary>
    public static readonly UcliErrorCode PrecompiledAssemblyConflict = new("PRECOMPILED_ASSEMBLY_CONFLICT");
}
