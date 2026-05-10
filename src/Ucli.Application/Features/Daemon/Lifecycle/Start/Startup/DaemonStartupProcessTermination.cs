namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;

/// <summary> Represents process termination details for one daemon startup attempt. </summary>
internal sealed record DaemonStartupProcessTermination (
    bool AttemptedGracefulShutdown,
    bool GracefulShutdownTimedOut,
    bool ForceKillAttempted,
    bool? ForceKillSucceeded,
    int? ExitCode,
    long ElapsedMilliseconds);
