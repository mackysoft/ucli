using MackySoft.Ucli.Application.Features.Status.Common.Contracts;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Observation;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Preflight;

namespace MackySoft.Ucli.Application.Features.Status.UseCases.Status;

/// <summary> Implements status workflow orchestration by composing preflight context and daemon observation results. </summary>
internal sealed class StatusService : IStatusService
{
    private readonly IStatusExecutionContextResolver statusExecutionContextResolver;

    private readonly IStatusDaemonObservationService statusDaemonObservationService;

    /// <summary> Initializes a new instance of the <see cref="StatusService" /> class. </summary>
    /// <param name="statusExecutionContextResolver"> The status execution-context resolver dependency. </param>
    /// <param name="statusDaemonObservationService"> The daemon observation service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public StatusService (
        IStatusExecutionContextResolver statusExecutionContextResolver,
        IStatusDaemonObservationService statusDaemonObservationService)
    {
        this.statusExecutionContextResolver = statusExecutionContextResolver ?? throw new ArgumentNullException(nameof(statusExecutionContextResolver));
        this.statusDaemonObservationService = statusDaemonObservationService ?? throw new ArgumentNullException(nameof(statusDaemonObservationService));
    }

    /// <summary> Executes one status workflow and returns normalized status output values. </summary>
    /// <param name="input"> The normalized status command input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the status execution result. </returns>
    public async ValueTask<StatusExecutionResult> ExecuteAsync (
        StatusCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var executionContextResult = await statusExecutionContextResolver.ResolveAsync(
                input,
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionContextResult.IsSuccess)
        {
            return StatusExecutionResult.Failure(executionContextResult.Error!);
        }

        var executionContext = executionContextResult.Context!;
        var daemonObservationResult = await statusDaemonObservationService.ObserveAsync(
                executionContext.Context,
                executionContext.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!daemonObservationResult.IsSuccess)
        {
            return StatusExecutionResult.Failure(daemonObservationResult.Error!);
        }

        var daemonObservation = daemonObservationResult.Observation!;

        var output = new StatusExecutionOutput(
            DaemonStatus: daemonObservation.DaemonStatus,
            UnityVersion: executionContext.UnityVersion,
            ServerVersion: daemonObservation.ServerVersion,
            LifecycleState: daemonObservation.LifecycleState,
            BlockingReason: daemonObservation.BlockingReason,
            CompileState: daemonObservation.CompileState,
            CompileGeneration: daemonObservation.CompileGeneration,
            DomainReloadGeneration: daemonObservation.DomainReloadGeneration,
            CanAcceptExecutionRequests: daemonObservation.CanAcceptExecutionRequests,
            EditorMode: daemonObservation.EditorMode,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds),
            ObservedAtUtc: daemonObservation.ObservedAtUtc,
            ActionRequired: daemonObservation.ActionRequired,
            PrimaryDiagnostic: daemonObservation.PrimaryDiagnostic,
            PlayMode: daemonObservation.PlayMode);
        return StatusExecutionResult.Success(output);
    }
}
