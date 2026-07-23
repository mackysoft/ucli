using System.Text.Json.Serialization;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Contracts.Ipc;
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
    /// <param name="EditorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="OnStartupBlocked"> The requested startup-blocked process policy. </param>
    internal sealed record EnsureRunningRequest (
        string UnityProjectRoot,
        ProjectFingerprint ProjectFingerprint,
        DaemonEditorMode? EditorMode,
        [property: JsonRequired]
        DaemonStartupBlockedProcessPolicy OnStartupBlocked);

    /// <summary> Represents the payload returned after one ensure-running request. </summary>
    /// <param name="StartStatus"> The daemon start status. </param>
    /// <param name="Session"> The active daemon session. </param>
    /// <param name="LifecycleObservation"> The endpoint-registered lifecycle observation when available. </param>
    internal sealed record EnsureRunningResponse (
        [property: JsonRequired]
        DaemonStartStatus StartStatus,
        DaemonSessionJsonContract Session,
        IpcUnityEditorObservation? LifecycleObservation);

    /// <summary> Represents the payload returned when ensure-running fails with optional startup metadata. </summary>
    /// <param name="DaemonStatus"> The daemon status after the failed start attempt. </param>
    /// <param name="Diagnosis"> The daemon diagnosis attached to the start failure when available. </param>
    /// <param name="Startup"> The startup observation attached to the start failure when available. </param>
    internal sealed record EnsureRunningFailureResponse (
        DaemonStatusKind? DaemonStatus,
        EnsureRunningFailureDiagnosis? Diagnosis,
        EnsureRunningFailureStartupObservation? Startup);

    /// <summary>
    /// Carries diagnosis values over supervisor IPC while representing filesystem paths as transport strings.
    /// </summary>
    internal sealed record EnsureRunningFailureDiagnosis (
        DaemonDiagnosisReason Reason,
        string Message,
        DaemonDiagnosisReportedBy ReportedBy,
        bool IsInferred,
        DateTimeOffset UpdatedAtUtc,
        int? ProcessId,
        string? EditorInstancePath,
        DateTimeOffset SessionIssuedAtUtc,
        DateTimeOffset? ProcessStartedAtUtc,
        string? UnityLogPath,
        DaemonDiagnosisStartupPhase? StartupPhase,
        DaemonDiagnosisActionRequired? ActionRequired,
        EnsureRunningFailurePrimaryDiagnostic? PrimaryDiagnostic);

    /// <summary> Carries the optional structured cause embedded in a transported diagnosis. </summary>
    internal sealed record EnsureRunningFailurePrimaryDiagnostic (
        DaemonDiagnosisPrimaryDiagnosticKind Kind,
        string? Code,
        string? File,
        int? Line,
        int? Column,
        string? Message);

    /// <summary>
    /// Carries startup observation values over supervisor IPC while representing the artifact path as transport text.
    /// </summary>
    internal sealed record EnsureRunningFailureStartupObservation (
        DaemonStartupStatus StartupStatus,
        DaemonStartupBlockingReason StartupBlockingReason,
        Guid? LaunchAttemptId,
        DaemonStartupProcessAction ProcessAction,
        DaemonStartupRetryDisposition RetryDisposition,
        DaemonEditorMode? EditorMode,
        DaemonSessionOwnerKind? OwnerKind,
        bool? CanShutdownProcess,
        int? ProcessId,
        DateTimeOffset? StartedAtUtc,
        int? ElapsedMilliseconds,
        string? ArtifactPath);

    /// <summary> Represents the payload used to stop one Unity daemon. </summary>
    /// <param name="UnityProjectRoot"> The absolute Unity project root path. </param>
    /// <param name="ProjectFingerprint"> The Unity project fingerprint. </param>
    internal sealed record StopProjectRequest (
        string UnityProjectRoot,
        ProjectFingerprint ProjectFingerprint);

    /// <summary> Represents the payload returned after one stop-project request. </summary>
    /// <param name="StopStatus"> The daemon stop status. </param>
    internal sealed record StopProjectResponse (
        [property: JsonRequired]
        DaemonStopStatus StopStatus);
}
