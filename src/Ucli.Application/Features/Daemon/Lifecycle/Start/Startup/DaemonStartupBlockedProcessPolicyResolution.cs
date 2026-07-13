namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;

/// <summary> Represents the resolved process handling for one startup-blocked daemon process. </summary>
/// <param name="ShouldTerminateProcess"> Whether uCLI should try to terminate the process. </param>
/// <param name="ProcessActionWhenNotTerminated"> The process action to report when no termination is attempted. </param>
internal sealed record DaemonStartupBlockedProcessPolicyResolution (
    bool ShouldTerminateProcess,
    DaemonStartupProcessAction ProcessActionWhenNotTerminated);
