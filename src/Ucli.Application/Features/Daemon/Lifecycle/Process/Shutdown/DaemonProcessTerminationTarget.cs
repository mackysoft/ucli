namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Shutdown;

/// <summary> Represents one daemon process target that uCLI is allowed to terminate. </summary>
/// <param name="ProcessId"> The daemon process identifier. </param>
/// <param name="ProcessStartedAtUtc"> The expected daemon process start timestamp when available. </param>
internal readonly record struct DaemonProcessTerminationTarget (
    int ProcessId,
    DateTimeOffset? ProcessStartedAtUtc);
