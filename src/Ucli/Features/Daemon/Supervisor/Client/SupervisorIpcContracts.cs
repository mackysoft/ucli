using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Client;

/// <summary> Defines internal IPC method names and payload models used by the supervisor runtime. </summary>
internal static class SupervisorIpcContracts
{
    /// <summary> Gets the method name used for supervisor health probing. </summary>
    public const string PingMethod = "supervisor.ping";

    /// <summary> Gets the method name used for ensuring one Unity daemon is running. </summary>
    public const string EnsureRunningMethod = "supervisor.ensureRunning";

    /// <summary> Gets the method name used for stopping one Unity daemon. </summary>
    public const string StopProjectMethod = "supervisor.stopProject";

    /// <summary> Represents the payload used by supervisor health probes. </summary>
    /// <param name="ClientVersion"> The caller client-version literal. </param>
    internal sealed record PingRequest (
        string ClientVersion);

    /// <summary> Represents the payload returned by supervisor health probes. </summary>
    /// <param name="ProcessId"> The supervisor process identifier. </param>
    /// <param name="IssuedAtUtc"> The supervisor start timestamp. </param>
    internal sealed record PingResponse (
        int ProcessId,
        DateTimeOffset IssuedAtUtc);

    /// <summary> Represents the payload used to ensure one Unity daemon is running. </summary>
    /// <param name="UnityProjectRoot"> The absolute Unity project root path. </param>
    /// <param name="ProjectFingerprint"> The Unity project fingerprint. </param>
    /// <param name="TimeoutMilliseconds"> The command timeout in milliseconds. </param>
    /// <param name="EditorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="OnStartupBlocked"> The requested startup-blocked process policy. </param>
    internal sealed record EnsureRunningRequest (
        string UnityProjectRoot,
        string ProjectFingerprint,
        int TimeoutMilliseconds,
        string? EditorMode,
        string OnStartupBlocked);

    /// <summary> Represents the payload returned after one ensure-running request. </summary>
    /// <param name="StartStatus"> The daemon start-status literal. </param>
    /// <param name="DaemonStatus"> The daemon status literal. </param>
    /// <param name="Session"> The active daemon session. </param>
    internal sealed record EnsureRunningResponse (
        string StartStatus,
        string DaemonStatus,
        DaemonSession Session);

    /// <summary> Represents the payload returned when ensure-running fails with optional startup metadata. </summary>
    /// <param name="Diagnosis"> The daemon diagnosis attached to the start failure when available. </param>
    /// <param name="Startup"> The startup observation attached to the start failure when available. </param>
    internal sealed record EnsureRunningFailureResponse (
        DaemonDiagnosis? Diagnosis,
        DaemonStartupObservation? Startup = null);

    /// <summary> Represents the payload used to stop one Unity daemon. </summary>
    /// <param name="UnityProjectRoot"> The absolute Unity project root path. </param>
    /// <param name="ProjectFingerprint"> The Unity project fingerprint. </param>
    /// <param name="TimeoutMilliseconds"> The command timeout in milliseconds. </param>
    internal sealed record StopProjectRequest (
        string UnityProjectRoot,
        string ProjectFingerprint,
        int TimeoutMilliseconds);

    /// <summary> Represents the payload returned after one stop-project request. </summary>
    /// <param name="StopStatus"> The daemon stop-status literal. </param>
    /// <param name="DaemonStatus"> The daemon status literal after stop processing. </param>
    internal sealed record StopProjectResponse (
        string StopStatus,
        string DaemonStatus);
}
