using System.Diagnostics.CodeAnalysis;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Contracts;

/// <summary> Maps ensure-running failure metadata across the supervisor IPC text boundary. </summary>
internal static class SupervisorEnsureRunningFailurePayloadMapper
{
    /// <summary> Converts guarded domain metadata to its supervisor IPC representation. </summary>
    public static SupervisorIpcContracts.EnsureRunningFailureResponse ToContract (
        DaemonStatusKind? daemonStatus,
        DaemonDiagnosis? diagnosis,
        DaemonStartupObservation? startup)
    {
        return new SupervisorIpcContracts.EnsureRunningFailureResponse(
            daemonStatus,
            diagnosis is null
                ? null
                : new SupervisorIpcContracts.EnsureRunningFailureDiagnosis(
                    diagnosis.Reason,
                    diagnosis.Message,
                    diagnosis.ReportedBy,
                    diagnosis.IsInferred,
                    diagnosis.UpdatedAtUtc,
                    diagnosis.ProcessId,
                    diagnosis.EditorInstancePath?.Value,
                    diagnosis.SessionIssuedAtUtc,
                    diagnosis.ProcessStartedAtUtc,
                    diagnosis.UnityLogPath?.Value,
                    diagnosis.StartupPhase,
                    diagnosis.ActionRequired,
                    diagnosis.PrimaryDiagnostic is null
                        ? null
                        : new SupervisorIpcContracts.EnsureRunningFailurePrimaryDiagnostic(
                            diagnosis.PrimaryDiagnostic.Kind,
                            diagnosis.PrimaryDiagnostic.Code,
                            diagnosis.PrimaryDiagnostic.File,
                            diagnosis.PrimaryDiagnostic.Line,
                            diagnosis.PrimaryDiagnostic.Column,
                            diagnosis.PrimaryDiagnostic.Message)),
            startup is null
                ? null
                : new SupervisorIpcContracts.EnsureRunningFailureStartupObservation(
                    startup.StartupStatus,
                    startup.StartupBlockingReason,
                    startup.LaunchAttemptId,
                    startup.ProcessAction,
                    startup.RetryDisposition,
                    startup.EditorMode,
                    startup.OwnerKind,
                    startup.CanShutdownProcess,
                    startup.ProcessId,
                    startup.StartedAtUtc,
                    startup.ElapsedMilliseconds,
                    startup.ArtifactPath?.Value));
    }

    /// <summary>
    /// Attempts to reconstruct guarded domain metadata from one supervisor IPC representation.
    /// </summary>
    /// <remarks>
    /// Path text is parsed only at this transport boundary. A successful result contains only
    /// path objects whose structural invariants were established by <see cref="AbsolutePath" />.
    /// </remarks>
    /// <returns>
    /// <see langword="true" /> when all transported values satisfy their domain contracts;
    /// otherwise <see langword="false" /> and <paramref name="metadata" /> is <see langword="null" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contract" /> is <see langword="null" />. </exception>
    public static bool TryToMetadata (
        SupervisorIpcContracts.EnsureRunningFailureResponse contract,
        [NotNullWhen(true)] out SupervisorEnsureRunningFailureMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (contract.DaemonStatus.HasValue
            && !ContractLiteralCodec.IsDefined(contract.DaemonStatus.Value))
        {
            metadata = null;
            return false;
        }

        DaemonDiagnosis? diagnosis = null;
        if (contract.Diagnosis is not null)
        {
            if (!TryParseOptionalAbsolutePath(contract.Diagnosis.EditorInstancePath, out var editorInstancePath)
                || !TryParseOptionalAbsolutePath(contract.Diagnosis.UnityLogPath, out var unityLogPath))
            {
                metadata = null;
                return false;
            }

            try
            {
                diagnosis = new DaemonDiagnosis(
                    contract.Diagnosis.Reason,
                    contract.Diagnosis.Message,
                    contract.Diagnosis.ReportedBy,
                    contract.Diagnosis.IsInferred,
                    contract.Diagnosis.UpdatedAtUtc,
                    contract.Diagnosis.ProcessId,
                    editorInstancePath,
                    contract.Diagnosis.SessionIssuedAtUtc,
                    contract.Diagnosis.ProcessStartedAtUtc,
                    unityLogPath,
                    contract.Diagnosis.StartupPhase,
                    contract.Diagnosis.ActionRequired,
                    contract.Diagnosis.PrimaryDiagnostic is null
                        ? null
                        : new DaemonPrimaryDiagnostic(
                            contract.Diagnosis.PrimaryDiagnostic.Kind,
                            contract.Diagnosis.PrimaryDiagnostic.Code,
                            contract.Diagnosis.PrimaryDiagnostic.File,
                            contract.Diagnosis.PrimaryDiagnostic.Line,
                            contract.Diagnosis.PrimaryDiagnostic.Column,
                            contract.Diagnosis.PrimaryDiagnostic.Message));
            }
            catch (ArgumentException)
            {
                metadata = null;
                return false;
            }
        }

        DaemonStartupObservation? startup = null;
        if (contract.Startup is not null)
        {
            if (!TryParseOptionalAbsolutePath(contract.Startup.ArtifactPath, out var artifactPath))
            {
                metadata = null;
                return false;
            }

            try
            {
                startup = new DaemonStartupObservation(
                    contract.Startup.StartupStatus,
                    contract.Startup.StartupBlockingReason,
                    contract.Startup.LaunchAttemptId,
                    contract.Startup.ProcessAction,
                    contract.Startup.RetryDisposition,
                    contract.Startup.EditorMode,
                    contract.Startup.OwnerKind,
                    contract.Startup.CanShutdownProcess,
                    contract.Startup.ProcessId,
                    contract.Startup.StartedAtUtc,
                    contract.Startup.ElapsedMilliseconds,
                    artifactPath);
            }
            catch (ArgumentException)
            {
                metadata = null;
                return false;
            }
        }

        metadata = new SupervisorEnsureRunningFailureMetadata(
            contract.DaemonStatus,
            diagnosis,
            startup);
        return true;
    }

    private static bool TryParseOptionalAbsolutePath (
        string? value,
        out AbsolutePath? path)
    {
        if (value is null)
        {
            path = null;
            return true;
        }

        return AbsolutePath.TryParse(value, out path, out _);
    }
}

/// <summary>
/// Carries validated supervisor failure metadata whose non-null filesystem paths are guarded path objects.
/// </summary>
internal sealed record SupervisorEnsureRunningFailureMetadata (
    DaemonStatusKind? DaemonStatus,
    DaemonDiagnosis? Diagnosis,
    DaemonStartupObservation? Startup);
