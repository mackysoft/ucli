using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

/// <summary> Implements daemon status workflow orchestration for one project fingerprint. </summary>
internal sealed class DaemonStatusOperation : IDaemonStatusOperation
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonDiagnosisStore daemonDiagnosisStore;

    private readonly IDaemonPingClient daemonPingClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly IDaemonSessionDiagnosisResolver daemonSessionDiagnosisResolver;

    /// <summary> Initializes a new instance of the <see cref="DaemonStatusOperation" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="daemonDiagnosisStore"> The daemon diagnosis store dependency. </param>
    /// <param name="daemonPingClient"> The daemon ping client dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability classifier dependency. </param>
    /// <param name="daemonSessionDiagnosisResolver"> The daemon session-diagnosis resolver dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStatusOperation (
        IDaemonSessionStore daemonSessionStore,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonSessionDiagnosisResolver daemonSessionDiagnosisResolver)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.daemonSessionDiagnosisResolver = daemonSessionDiagnosisResolver ?? throw new ArgumentNullException(nameof(daemonSessionDiagnosisResolver));
    }

    /// <summary> Gets daemon status for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon status timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon status result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStatusResult> GetStatus (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var diagnosisReadResult = await daemonDiagnosisStore.Read(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        var persistedDiagnosis = diagnosisReadResult.IsSuccess
            ? diagnosisReadResult.Diagnosis
            : null;

        var readResult = await daemonSessionStore.Read(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return DaemonStatusResult.Failure(readResult.Error!);
        }

        if (!readResult.Exists)
        {
            return DaemonStatusResult.NotRunning(persistedDiagnosis);
        }

        try
        {
            await daemonPingClient.Ping(
                    unityProject,
                    timeout,
                    readResult.Session!.SessionToken,
                    cancellationToken)
                .ConfigureAwait(false);
            return DaemonStatusResult.Running(readResult.Session!, persistedDiagnosis);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return DaemonStatusResult.Failure(ExecutionError.Timeout(
                $"Timed out while probing daemon status. {exception.Message}"));
        }
        catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
        {
            var staleDiagnosis = await daemonSessionDiagnosisResolver.ResolveForSession(
                    unityProject,
                    readResult.Session!,
                    persistedDiagnosis,
                    cancellationToken)
                .ConfigureAwait(false);
            return DaemonStatusResult.Stale(
                readResult.Session!,
                staleDiagnosis);
        }
        catch (Exception exception)
        {
            return DaemonStatusResult.Failure(ExecutionError.InternalError(
                $"Failed to probe daemon status. {exception.Message}"));
        }
    }
}
