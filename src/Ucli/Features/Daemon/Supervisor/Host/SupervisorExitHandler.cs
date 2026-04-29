using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

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
    public async Task HandleExit (
        SupervisorManagedDaemonProcess managedProcess,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(managedProcess);

        var currentSessionRead = await daemonSessionStore.Read(
                managedProcess.UnityProject.RepositoryRoot,
                managedProcess.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        var shouldWriteUnexpectedDiagnosis = false;
        if (currentSessionRead.IsSuccess && currentSessionRead.Exists)
        {
            var currentSession = currentSessionRead.Session!;
            if (!SupervisorSessionIdentity.IsSameSession(currentSession, managedProcess.Session))
            {
                return;
            }

            shouldWriteUnexpectedDiagnosis = !managedProcess.IsStopRequested;
        }
        else if (!currentSessionRead.IsSuccess)
        {
            await runtimeLogger.Write(
                    managedProcess.UnityProject.RepositoryRoot,
                    "error",
                    $"Supervisor session read failed during exit cleanup. fingerprint={managedProcess.UnityProject.ProjectFingerprint} kind={currentSessionRead.FailureKind} error={currentSessionRead.Error!.Message}",
                    CancellationToken.None)
                .ConfigureAwait(false);
        }

        if (shouldWriteUnexpectedDiagnosis)
        {
            var diagnosisWriteResult = await diagnosisWriter.WriteUnexpected(
                    managedProcess.UnityProject,
                    managedProcess.Session,
                    DaemonDiagnosisReasonValues.UnexpectedExit,
                    $"Unity daemon process exited unexpectedly. ProcessId={managedProcess.ProcessId}.",
                    cancellationToken)
                .ConfigureAwait(false);
            if (!diagnosisWriteResult.IsSuccess)
            {
                await runtimeLogger.Write(
                        managedProcess.UnityProject.RepositoryRoot,
                        "error",
                        $"Supervisor diagnosis write failed after daemon exit. fingerprint={managedProcess.UnityProject.ProjectFingerprint} error={diagnosisWriteResult.Error!.Message}",
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        var cleanupResult = await daemonArtifactCleaner.Cleanup(
                managedProcess.UnityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (!cleanupResult.IsSuccess)
        {
            await runtimeLogger.Write(
                    managedProcess.UnityProject.RepositoryRoot,
                    "error",
                    $"Supervisor artifact cleanup failed after daemon exit. fingerprint={managedProcess.UnityProject.ProjectFingerprint} error={cleanupResult.Error!.Message}",
                    CancellationToken.None)
                .ConfigureAwait(false);
        }

        await runtimeLogger.Write(
                managedProcess.UnityProject.RepositoryRoot,
                managedProcess.IsStopRequested ? "info" : "warning",
                $"Unity daemon exited. fingerprint={managedProcess.UnityProject.ProjectFingerprint} pid={managedProcess.ProcessId?.ToString() ?? "unknown"} stopRequested={managedProcess.IsStopRequested}",
                CancellationToken.None)
            .ConfigureAwait(false);
    }
}
