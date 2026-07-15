using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Finalizes supervisor-owned daemon artifacts after one managed process exits. </summary>
internal sealed class SupervisorExitHandler
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonArtifactCleaner daemonArtifactCleaner;

    private readonly SupervisorDiagnosisWriter diagnosisWriter;

    private readonly SupervisorRuntimeLogger runtimeLogger;

    /// <summary> Initializes a new instance of the <see cref="SupervisorExitHandler" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session-store dependency. </param>
    /// <param name="daemonArtifactCleaner"> The daemon artifact-cleaner dependency. </param>
    /// <param name="diagnosisWriter"> The supervisor diagnosis-writer dependency. </param>
    /// <param name="runtimeLogger"> The supervisor runtime-logger dependency. </param>
    public SupervisorExitHandler (
        IDaemonSessionStore daemonSessionStore,
        IDaemonArtifactCleaner daemonArtifactCleaner,
        SupervisorDiagnosisWriter diagnosisWriter,
        SupervisorRuntimeLogger runtimeLogger)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonArtifactCleaner = daemonArtifactCleaner ?? throw new ArgumentNullException(nameof(daemonArtifactCleaner));
        this.diagnosisWriter = diagnosisWriter ?? throw new ArgumentNullException(nameof(diagnosisWriter));
        this.runtimeLogger = runtimeLogger ?? throw new ArgumentNullException(nameof(runtimeLogger));
    }

    /// <summary> Handles one managed Unity daemon process exit. </summary>
    /// <param name="managedProcess"> The managed process that exited. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    public async Task HandleExitAsync (
        SupervisorManagedDaemonProcess managedProcess,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(managedProcess);

        if (!managedProcess.Session.CanShutdownProcess)
        {
            return;
        }

        var hasStoppedProcess = DaemonSessionTerminationPolicy.TryGetTerminationTarget(
            managedProcess.Session,
            out var stoppedProcess);
        var currentSessionRead = await daemonSessionStore.ReadAsync(
                managedProcess.UnityProject.RepositoryRoot,
                managedProcess.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        var shouldWriteUnexpectedDiagnosis = false;
        string? sessionReadFailureMessage = null;
        if (currentSessionRead.IsSuccess && currentSessionRead.Exists)
        {
            var currentSession = currentSessionRead.Session!;
            if (!SupervisorSessionIdentity.IsSameSession(currentSession, managedProcess.Session))
            {
                if (!hasStoppedProcess || !MatchesStoppedProcess(currentSession, stoppedProcess))
                {
                    return;
                }
            }

            shouldWriteUnexpectedDiagnosis = !managedProcess.IsStopRequested;
        }
        else if (!currentSessionRead.IsSuccess)
        {
            sessionReadFailureMessage =
                $"Supervisor session read failed during exit cleanup. fingerprint={managedProcess.UnityProject.ProjectFingerprint} kind={currentSessionRead.FailureKind} error={currentSessionRead.Error!.Message}";
        }

        var cleanupResult = hasStoppedProcess
            ? await daemonArtifactCleaner.CleanupIfStoppedProcessMatchesAsync(
                    managedProcess.UnityProject,
                    stoppedProcess,
                    cancellationToken)
                .ConfigureAwait(false)
            : await daemonArtifactCleaner.CleanupIfSessionMatchesAsync(
                    managedProcess.UnityProject,
                    managedProcess.Session,
                    cancellationToken)
                .ConfigureAwait(false);
        var cleanupFailureMessage = cleanupResult.IsSuccess
            ? null
            : $"Supervisor artifact cleanup failed after daemon exit. fingerprint={managedProcess.UnityProject.ProjectFingerprint} error={cleanupResult.Error!.Message}";

        string? diagnosisFailureMessage = null;
        if (shouldWriteUnexpectedDiagnosis)
        {
            var diagnosisWriteResult = await diagnosisWriter.WriteUnexpectedAsync(
                    managedProcess.UnityProject,
                    managedProcess.Session,
                    DaemonDiagnosisReason.UnexpectedExit,
                    $"Unity daemon process exited unexpectedly. ProcessId={managedProcess.ProcessId}.",
                    cancellationToken)
                .ConfigureAwait(false);
            if (!diagnosisWriteResult.IsSuccess)
            {
                diagnosisFailureMessage =
                    $"Supervisor diagnosis write failed after daemon exit. fingerprint={managedProcess.UnityProject.ProjectFingerprint} error={diagnosisWriteResult.Error!.Message}";
            }
        }

        if (sessionReadFailureMessage is not null)
        {
            await runtimeLogger.WriteAsync(
                    managedProcess.UnityProject.RepositoryRoot,
                    "error",
                    sessionReadFailureMessage,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }

        if (diagnosisFailureMessage is not null)
        {
            await runtimeLogger.WriteAsync(
                    managedProcess.UnityProject.RepositoryRoot,
                    "error",
                    diagnosisFailureMessage,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }

        if (cleanupFailureMessage is not null)
        {
            await runtimeLogger.WriteAsync(
                    managedProcess.UnityProject.RepositoryRoot,
                    "error",
                    cleanupFailureMessage,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }

        await runtimeLogger.WriteAsync(
                managedProcess.UnityProject.RepositoryRoot,
                managedProcess.IsStopRequested ? "info" : "warning",
                $"Unity daemon exited. fingerprint={managedProcess.UnityProject.ProjectFingerprint} pid={managedProcess.ProcessId?.ToString() ?? "unknown"} stopRequested={managedProcess.IsStopRequested}",
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static bool MatchesStoppedProcess (
        DaemonSession session,
        DaemonProcessTerminationTarget stoppedProcess)
    {
        return session.ProcessId == stoppedProcess.ProcessId
            && session.ProcessStartedAtUtc == stoppedProcess.ProcessStartedAtUtc;
    }
}
