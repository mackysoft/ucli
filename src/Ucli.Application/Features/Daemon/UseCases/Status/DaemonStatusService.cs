using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Projection;

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;

/// <summary> Implements daemon-status command workflow orchestration. </summary>
internal sealed class DaemonStatusService : IDaemonStatusService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IDaemonStatusOperation daemonStatusOperation;

    private readonly IDaemonSessionOutputMapper daemonSessionOutputMapper;

    private readonly IDaemonDiagnosisOutputMapper daemonDiagnosisOutputMapper;

    /// <summary> Initializes a new instance of the <see cref="DaemonStatusService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command execution-context resolver dependency. </param>
    /// <param name="daemonStatusOperation"> The daemon status-operation dependency. </param>
    /// <param name="daemonSessionOutputMapper"> The daemon session-output mapper dependency. </param>
    /// <param name="daemonDiagnosisOutputMapper"> The daemon diagnosis-output mapper dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStatusService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IDaemonStatusOperation daemonStatusOperation,
        IDaemonSessionOutputMapper daemonSessionOutputMapper,
        IDaemonDiagnosisOutputMapper daemonDiagnosisOutputMapper)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonStatusOperation = daemonStatusOperation ?? throw new ArgumentNullException(nameof(daemonStatusOperation));
        this.daemonSessionOutputMapper = daemonSessionOutputMapper ?? throw new ArgumentNullException(nameof(daemonSessionOutputMapper));
        this.daemonDiagnosisOutputMapper = daemonDiagnosisOutputMapper ?? throw new ArgumentNullException(nameof(daemonDiagnosisOutputMapper));
    }

    /// <summary> Executes one daemon-status workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeoutMilliseconds"> The optional normalized timeout value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-status execution result. </returns>
    public async ValueTask<DaemonStatusExecutionResult> GetStatusAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.ResolveAsync(
                UcliCommandIds.DaemonStatus,
                projectPath,
                timeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return DaemonStatusExecutionResult.Failure(contextResult.Error!);
        }

        var executionContext = contextResult.Context!;
        var statusResult = await daemonStatusOperation.GetStatusAsync(
                executionContext.Context.UnityProject,
                executionContext.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!statusResult.IsSuccess)
        {
            return DaemonStatusExecutionResult.Failure(statusResult.Error);
        }

        var status = statusResult.Status.Value;
        var daemonObservation = StatusDaemonObservationCodec.CreateWithoutPing(status);
        var diagnosis = status == DaemonStatusKind.Running
            ? null
            : statusResult.Diagnosis;

        if (status == DaemonStatusKind.Running)
        {
            daemonObservation = StatusDaemonObservationCodec.CreateFromPing(
                status,
                statusResult.PingResponse!);
        }

        var output = new DaemonStatusExecutionOutput(
            DaemonStatus: daemonObservation.DaemonStatus,
            ServerVersion: daemonObservation.ServerVersion,
            EditorMode: daemonObservation.EditorMode,
            LifecycleState: daemonObservation.LifecycleState,
            BlockingReason: daemonObservation.BlockingReason,
            CompileState: daemonObservation.CompileState,
            Generations: daemonObservation.Generations,
            CanAcceptExecutionRequests: daemonObservation.CanAcceptExecutionRequests,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds),
            Session: statusResult.Session is null
                ? null
                : daemonSessionOutputMapper.ToOutput(statusResult.Session),
            Diagnosis: diagnosis is null
                ? null
                : daemonDiagnosisOutputMapper.ToOutput(diagnosis),
            LastLaunchAttempt: statusResult.LastLaunchAttempt is null || statusResult.Session is not null
                ? null
                : ToLaunchAttemptOutput(statusResult.LastLaunchAttempt),
            ObservedAtUtc: daemonObservation.ObservedAtUtc,
            ActionRequired: daemonObservation.ActionRequired,
            PrimaryDiagnostic: daemonObservation.PrimaryDiagnostic,
            PlayMode: daemonObservation.PlayMode);
        return DaemonStatusExecutionResult.Success(output);
    }

    private DaemonLaunchAttemptOutput ToLaunchAttemptOutput (DaemonLaunchAttempt launchAttempt)
    {
        ArgumentNullException.ThrowIfNull(launchAttempt);
        return new DaemonLaunchAttemptOutput(
            LaunchAttemptId: launchAttempt.LaunchAttemptId,
            StartupStatus: launchAttempt.StartupStatus,
            StartupBlockingReason: launchAttempt.StartupBlockingReason,
            RetryDisposition: launchAttempt.RetryDisposition,
            ProcessAction: launchAttempt.ProcessAction,
            ArtifactPath: launchAttempt.ArtifactPath.Value,
            UnityLogPath: launchAttempt.UnityLogPath?.Value,
            UpdatedAtUtc: launchAttempt.UpdatedAtUtc,
            ProcessId: launchAttempt.ProcessId,
            ProcessStartedAtUtc: launchAttempt.ProcessStartedAtUtc,
            Diagnosis: daemonDiagnosisOutputMapper.ToOutput(launchAttempt.Diagnosis));
    }

}
