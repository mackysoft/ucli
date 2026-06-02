using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines daemon-start progress event literals. </summary>
public enum DaemonStartProgressEvent
{
    /// <summary> Daemon-start context resolution completed. </summary>
    [UcliContractLiteral("daemon.start.started")]
    Started = 0,

    /// <summary> uCLI Unity plugin verification started. </summary>
    [UcliContractLiteral("daemon.start.pluginVerification.started")]
    PluginVerificationStarted = 1,

    /// <summary> uCLI Unity plugin verification completed. </summary>
    [UcliContractLiteral("daemon.start.pluginVerification.completed")]
    PluginVerificationCompleted = 2,

    /// <summary> Supervisor bootstrap started. </summary>
    [UcliContractLiteral("daemon.start.supervisorBootstrap.started")]
    SupervisorBootstrapStarted = 3,

    /// <summary> Supervisor bootstrap completed. </summary>
    [UcliContractLiteral("daemon.start.supervisorBootstrap.completed")]
    SupervisorBootstrapCompleted = 4,

    /// <summary> Supervisor ensure-running request started. </summary>
    [UcliContractLiteral("daemon.start.ensureRunning.started")]
    EnsureRunningStarted = 5,

    /// <summary> Supervisor ensure-running request completed. </summary>
    [UcliContractLiteral("daemon.start.ensureRunning.completed")]
    EnsureRunningCompleted = 6,

    /// <summary> Supervisor started launching a Unity Editor process. </summary>
    [UcliContractLiteral("daemon.start.launching")]
    Launching = 7,

    /// <summary> Supervisor started waiting for daemon endpoint registration. </summary>
    [UcliContractLiteral("daemon.start.waitingForEndpoint")]
    WaitingForEndpoint = 8,

    /// <summary> Supervisor detected a terminal startup blocker. </summary>
    [UcliContractLiteral("daemon.start.blockerDetected")]
    BlockerDetected = 9,

    /// <summary> Supervisor observed daemon session registration. </summary>
    [UcliContractLiteral("daemon.start.sessionRegistered")]
    SessionRegistered = 10,

    /// <summary> Supervisor observed daemon endpoint registration. </summary>
    [UcliContractLiteral("daemon.start.endpointRegistered")]
    EndpointRegistered = 11,

    /// <summary> Supervisor observed an endpoint-registered lifecycle snapshot. </summary>
    [UcliContractLiteral("daemon.start.lifecycleObserved")]
    LifecycleObserved = 12,

    /// <summary> Daemon-start final output was determined. </summary>
    [UcliContractLiteral("daemon.start.completed")]
    Completed = 13,
}
