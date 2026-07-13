using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary> Defines internal constants used by the worktree-local supervisor runtime. </summary>
internal static class SupervisorConstants
{
    /// <summary> Gets the hidden command-line flag used to run the supervisor host. </summary>
    public const string InternalServeFlag = "--ucli-internal-supervisor-serve";

    /// <summary> Gets the hidden option name used to pass the repository root to the supervisor host. </summary>
    public const string RepositoryRootOption = "--repositoryRoot";

    /// <summary> Gets the ping client-version literal used for supervisor health probes. </summary>
    public const string PingClientVersion = "ucli-supervisor-bootstrap";

    /// <summary> Gets the retry delay used while waiting for bootstrap completion. </summary>
    public static readonly TimeSpan BootstrapPollDelay = TimeSpan.FromMilliseconds(100);

    /// <summary> Gets the grace period allowed for a launched supervisor to publish a reachable manifest. </summary>
    public static readonly TimeSpan ManifestPublicationTimeout = TimeSpan.FromSeconds(15);

    /// <summary> Gets the maximum time allowed for one supervisor manifest mutation lock acquisition. </summary>
    public static readonly TimeSpan ManifestMutationLockTimeout = TimeSpan.FromSeconds(1);

    /// <summary> Gets the maximum time allowed for one supervisor host to claim runtime ownership. </summary>
    public static readonly TimeSpan RuntimeOwnershipLockTimeout = TimeSpan.FromSeconds(1);

    /// <summary> Gets the idle delay after which an unused supervisor exits. </summary>
    public static readonly TimeSpan IdleShutdownDelay = TimeSpan.FromSeconds(10);

    /// <summary> Gets the time budget used for one supervisor ping attempt. </summary>
    public static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(1);

    /// <summary> Gets the maximum time allowed for a connected supervisor client to send its first request frame. </summary>
    public static readonly TimeSpan InitialFrameReadTimeout = TimeSpan.FromSeconds(5);

    /// <summary> Gets the maximum number of supervisor connections handled concurrently. </summary>
    public const int MaximumActiveConnections = 32;

    /// <summary> Gets the maximum time allowed for accepted supervisor connections to drain during shutdown. </summary>
    public static readonly TimeSpan ConnectionDrainTimeout = TimeSpan.FromSeconds(1);

    /// <summary> Gets the owned follow-up budget for persisting one supervisor stability diagnosis. </summary>
    public static readonly TimeSpan StabilityDiagnosisWriteTimeout = TimeSpan.FromSeconds(1);

    /// <summary> Gets the maximum time allowed for writing one supervisor response frame. </summary>
    public static readonly TimeSpan ResponseFrameWriteTimeout = TimeSpan.FromSeconds(1);

    /// <summary> Gets the maximum time allowed for one best-effort supervisor runtime-log write. </summary>
    public static readonly TimeSpan RuntimeLogWriteTimeout = TimeSpan.FromSeconds(1);

    /// <summary> Gets the grace period for receiving an ensure-running terminal response after its command timeout. </summary>
    public static readonly TimeSpan EnsureRunningTerminalResponseGrace =
        DaemonTimeouts.LaunchCompensationTimeout
        + DaemonTimeouts.SupplementalPersistenceTimeout
        + DaemonTimeouts.SupplementalPersistenceTimeout
        + ResponseFrameWriteTimeout;

    /// <summary> Gets the grace period for receiving a stop-project terminal response after its command deadline. </summary>
    public static readonly TimeSpan StopProjectTerminalResponseGrace =
        DaemonTimeouts.StopCompensationTimeout
        + ResponseFrameWriteTimeout;

    /// <summary> Gets the fixed stability window used after daemon reachability is first established. </summary>
    public static readonly TimeSpan StabilityWindow = TimeSpan.FromSeconds(2);

    /// <summary> Gets the required number of consecutive successful ping observations during the stability window. </summary>
    public const int StabilitySuccessCount = 3;

    /// <summary> Gets the polling delay used to monitor pid-less managed daemon sessions. </summary>
    public static readonly TimeSpan PidlessMonitorPollDelay = TimeSpan.FromMilliseconds(250);
}
