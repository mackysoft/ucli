using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Contracts;

/// <summary> Defines internal IPC payload models used by the supervisor runtime. </summary>
internal static class SupervisorIpcContracts
{
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
    /// <param name="DeadlineUtc"> The absolute UTC deadline shared by client delivery and server execution. </param>
    /// <param name="AttemptTimeoutMilliseconds"> The monotonic caller budget remaining when this delivery attempt starts. </param>
    /// <param name="EditorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="OnStartupBlocked"> The requested startup-blocked process policy. </param>
    internal sealed record EnsureRunningRequest (
        string UnityProjectRoot,
        string ProjectFingerprint,
        DateTimeOffset DeadlineUtc,
        int AttemptTimeoutMilliseconds,
        string? EditorMode,
        string OnStartupBlocked);

    /// <summary> Represents the payload returned after one ensure-running request. </summary>
    /// <param name="StartStatus"> The daemon start-status literal. </param>
    /// <param name="DaemonStatus"> The daemon status literal. </param>
    /// <param name="Session"> The active daemon session. </param>
    /// <param name="LifecycleSnapshot"> The endpoint-registered lifecycle snapshot when available. </param>
    internal sealed record EnsureRunningResponse (
        string StartStatus,
        string DaemonStatus,
        DaemonSessionJsonContract Session,
        DaemonStartLifecycleSnapshot? LifecycleSnapshot = null);

    /// <summary> Represents the payload returned when ensure-running fails with optional startup metadata. </summary>
    /// <param name="DaemonStatus"> The daemon status after the failed start attempt. </param>
    /// <param name="Diagnosis"> The daemon diagnosis attached to the start failure when available. </param>
    /// <param name="Startup"> The startup observation attached to the start failure when available. </param>
    internal sealed record EnsureRunningFailureResponse (
        string? DaemonStatus,
        DaemonDiagnosis? Diagnosis,
        DaemonStartupObservation? Startup = null);

    /// <summary> Represents the payload used to stop one Unity daemon. </summary>
    /// <param name="UnityProjectRoot"> The absolute Unity project root path. </param>
    /// <param name="ProjectFingerprint"> The Unity project fingerprint. </param>
    /// <param name="DeadlineUtc"> The shared absolute command deadline. </param>
    /// <param name="AttemptTimeoutMilliseconds"> The monotonic caller budget remaining when this delivery attempt starts. </param>
    internal sealed record StopProjectRequest (
        string UnityProjectRoot,
        string ProjectFingerprint,
        DateTimeOffset DeadlineUtc,
        int AttemptTimeoutMilliseconds);

    /// <summary> Represents the payload returned after one stop-project request. </summary>
    /// <param name="StopStatus"> The daemon stop-status literal. </param>
    /// <param name="DaemonStatus"> The daemon status literal after stop processing. </param>
    internal sealed record StopProjectResponse (
        string StopStatus,
        string DaemonStatus);
}
