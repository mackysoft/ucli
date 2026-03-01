using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Executes normalized operations through <c>validate -&gt; plan -&gt; call</c> phase pipelines. </summary>
    internal sealed class OperationPhaseExecutor : IOperationPhaseExecutor
    {
        private readonly IOperationPlanPassExecutor planPassExecutor;

        private readonly IOperationCallPassExecutor callPassExecutor;

        private readonly IPlanTokenCoordinator planTokenCoordinator;

        /// <summary> Initializes a new instance of the <see cref="OperationPhaseExecutor" /> class. </summary>
        /// <param name="operationRegistry"> The phase-operation registry dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationRegistry" /> is <see langword="null" />. </exception>
        public OperationPhaseExecutor (IPhaseOperationRegistry operationRegistry)
            : this(operationRegistry, new PlanTokenCoordinator())
        {
        }

        /// <summary> Initializes a new instance of the <see cref="OperationPhaseExecutor" /> class. </summary>
        /// <param name="operationRegistry"> The phase-operation registry dependency. </param>
        /// <param name="planTokenCoordinator"> The plan-token coordination dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public OperationPhaseExecutor (
            IPhaseOperationRegistry operationRegistry,
            IPlanTokenCoordinator planTokenCoordinator)
            : this(
                new OperationPlanPassExecutor(operationRegistry),
                new OperationCallPassExecutor(),
                planTokenCoordinator)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="OperationPhaseExecutor" /> class. </summary>
        /// <param name="planPassExecutor"> The validate/plan pass executor dependency. </param>
        /// <param name="callPassExecutor"> The call pass executor dependency. </param>
        /// <param name="planTokenCoordinator"> The plan-token coordination dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        internal OperationPhaseExecutor (
            IOperationPlanPassExecutor planPassExecutor,
            IOperationCallPassExecutor callPassExecutor,
            IPlanTokenCoordinator planTokenCoordinator)
        {
            this.planPassExecutor = planPassExecutor ?? throw new ArgumentNullException(nameof(planPassExecutor));
            this.callPassExecutor = callPassExecutor ?? throw new ArgumentNullException(nameof(callPassExecutor));
            this.planTokenCoordinator = planTokenCoordinator ?? throw new ArgumentNullException(nameof(planTokenCoordinator));
        }

        /// <summary> Executes one normalized request through the specified command phase-flow. </summary>
        /// <param name="command"> The top-level execution command. </param>
        /// <param name="request"> The normalized request. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The request-level execution trace. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        /// <exception cref="System.OperationCanceledException"> Thrown when execution is canceled. </exception>
        public async Task<PhaseExecutionTrace> Execute (
            PhaseExecutionCommand command,
            NormalizedExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var planPassResult = await planPassExecutor.Execute(request, cancellationToken).ConfigureAwait(false);
            if (!planPassResult.IsSuccess)
            {
                return PhaseExecutionTrace.Failure(
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    operationTraces: planPassResult.OperationTraces,
                    errors: planPassResult.Errors);
            }

            if (command == PhaseExecutionCommand.Plan)
            {
                var issueResult = planTokenCoordinator.Issue(request, planPassResult.OperationTraces, cancellationToken);
                if (!issueResult.IsSuccess)
                {
                    return PhaseExecutionTrace.Failure(
                        protocolVersion: request.ProtocolVersion,
                        requestId: request.RequestId,
                        operationTraces: planPassResult.OperationTraces,
                        errors: new[]
                        {
                            issueResult.Failure!,
                        });
                }

                return PhaseExecutionTrace.Success(
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    operationTraces: planPassResult.OperationTraces,
                    planToken: issueResult.PlanToken);
            }

            var validationResult = planTokenCoordinator.ValidateCall(request, planPassResult.OperationTraces, cancellationToken);
            if (!validationResult.IsSuccess)
            {
                return PhaseExecutionTrace.Failure(
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    operationTraces: planPassResult.OperationTraces,
                    errors: new[]
                    {
                        validationResult.Failure!,
                    });
            }

            var callPassResult = await callPassExecutor.Execute(planPassResult.PreparedOperations, cancellationToken).ConfigureAwait(false);
            return callPassResult.IsSuccess
                ? PhaseExecutionTrace.Success(request.ProtocolVersion, request.RequestId, callPassResult.OperationTraces)
                : PhaseExecutionTrace.Failure(request.ProtocolVersion, request.RequestId, callPassResult.OperationTraces, callPassResult.Errors);
        }
    }
}
