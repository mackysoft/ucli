namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Defines application-level failure classifications before CLI projection. </summary>
internal enum ApplicationFailureKind
{
    /// <summary> Indicates invalid user input or command arguments. </summary>
    InvalidInput = 0,

    /// <summary> Indicates invalid or unreadable uCLI configuration. </summary>
    ConfigurationError = 1,

    /// <summary> Indicates an environment prerequisite failure. </summary>
    EnvironmentError = 2,

    /// <summary> Indicates a Unity IPC execution failure. </summary>
    UnityIpcFailure = 3,

    /// <summary> Indicates an external process failure. </summary>
    ExternalProcessFailure = 4,

    /// <summary> Indicates an application or wire contract violation. </summary>
    ContractViolation = 5,

    /// <summary> Indicates timeout while waiting for an execution boundary. </summary>
    Timeout = 6,

    /// <summary> Indicates command execution cancellation. </summary>
    Canceled = 7,

    /// <summary> Indicates an unexpected internal failure. </summary>
    InternalError = 8,
}
