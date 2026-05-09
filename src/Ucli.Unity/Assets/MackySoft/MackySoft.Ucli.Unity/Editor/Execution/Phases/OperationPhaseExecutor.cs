using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
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

        private readonly IDangerousOperationCallAuthorizer dangerousOperationCallAuthorizer;

        /// <summary> Initializes a new instance of the <see cref="OperationPhaseExecutor" /> class. </summary>
        /// <param name="operationRegistry"> The phase-operation registry dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationRegistry" /> is <see langword="null" />. </exception>
        public OperationPhaseExecutor (IPhaseOperationRegistry operationRegistry)
            : this(operationRegistry, new PlanTokenCoordinator(), new DangerousOperationCallAuthorizer())
        {
        }

        /// <summary> Initializes a new instance of the <see cref="OperationPhaseExecutor" /> class. </summary>
        /// <param name="operationRegistry"> The phase-operation registry dependency. </param>
        /// <param name="planTokenCoordinator"> The plan-token coordination dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public OperationPhaseExecutor (
            IPhaseOperationRegistry operationRegistry,
            IPlanTokenCoordinator planTokenCoordinator)
            : this(operationRegistry, planTokenCoordinator, new DangerousOperationCallAuthorizer())
        {
        }

        internal OperationPhaseExecutor (
            IPhaseOperationRegistry operationRegistry,
            IPlanTokenCoordinator planTokenCoordinator,
            IDangerousOperationCallAuthorizer dangerousOperationCallAuthorizer)
            : this(
                new OperationPlanPassExecutor(operationRegistry),
                new OperationCallPassExecutor(),
                planTokenCoordinator,
                dangerousOperationCallAuthorizer)
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
            IPlanTokenCoordinator planTokenCoordinator,
            IDangerousOperationCallAuthorizer dangerousOperationCallAuthorizer)
        {
            this.planPassExecutor = planPassExecutor ?? throw new ArgumentNullException(nameof(planPassExecutor));
            this.callPassExecutor = callPassExecutor ?? throw new ArgumentNullException(nameof(callPassExecutor));
            this.planTokenCoordinator = planTokenCoordinator ?? throw new ArgumentNullException(nameof(planTokenCoordinator));
            this.dangerousOperationCallAuthorizer = dangerousOperationCallAuthorizer ?? throw new ArgumentNullException(nameof(dangerousOperationCallAuthorizer));
        }

        /// <summary> Executes one normalized request through the specified command phase-flow. </summary>
        /// <param name="command"> The top-level execution command. </param>
        /// <param name="request"> The normalized request. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The request-level execution trace. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        /// <exception cref="System.OperationCanceledException"> Thrown when execution is canceled. </exception>
        public async Task<PhaseExecutionTrace> ExecuteAsync (
            PhaseExecutionCommand command,
            NormalizedExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (command == PhaseExecutionCommand.Call)
            {
                var requestValidationResult = planTokenCoordinator.ValidateCallRequest(request, cancellationToken);
                if (!requestValidationResult.IsSuccess)
                {
                    return PhaseExecutionTrace.Failure(
                        protocolVersion: request.ProtocolVersion,
                        requestId: request.RequestId,
                        steps: CreateUncompiledSteps(request.SourceSteps),
                        operationTraces: Array.Empty<OperationPhaseTrace>(),
                        errors: new[]
                        {
                            requestValidationResult.Failure!,
                        });
                }
            }

            using var executionContext = new OperationExecutionContext();
            var operationPreflight = command == PhaseExecutionCommand.Call && !request.AllowDangerous
                ? CreateDangerousCallPreflight()
                : null;
            var planPassResult = await planPassExecutor.ExecuteAsync(request, executionContext, operationPreflight, cancellationToken).ConfigureAwait(false);
            if (!planPassResult.IsSuccess)
            {
                if (command == PhaseExecutionCommand.Call
                    && !string.IsNullOrWhiteSpace(request.PlanToken))
                {
                    var planTokenValidationResult = planTokenCoordinator.ValidateCall(
                        request,
                        planPassResult.OperationTraces,
                        planPassResult.CompiledDigestPayloadUtf8,
                        cancellationToken);
                    if (!planTokenValidationResult.IsSuccess)
                    {
                        return CreatePlanTokenValidationFailure(request, planPassResult, planTokenValidationResult.Failure!);
                    }
                }

                return PhaseExecutionTrace.Failure(
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    steps: planPassResult.CompiledSteps,
                    operationTraces: planPassResult.OperationTraces,
                    errors: planPassResult.Errors);
            }

            if (command == PhaseExecutionCommand.PlanWithoutToken)
            {
                return PhaseExecutionTrace.Success(
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    steps: planPassResult.CompiledSteps,
                    operationTraces: planPassResult.OperationTraces);
            }

            if (command == PhaseExecutionCommand.Plan)
            {
                var issueResult = planTokenCoordinator.Issue(
                    request,
                    planPassResult.OperationTraces,
                    planPassResult.CompiledDigestPayloadUtf8,
                    cancellationToken);
                if (!issueResult.IsSuccess)
                {
                    return PhaseExecutionTrace.Failure(
                        protocolVersion: request.ProtocolVersion,
                        requestId: request.RequestId,
                        steps: planPassResult.CompiledSteps,
                        operationTraces: planPassResult.OperationTraces,
                        errors: new[]
                        {
                            issueResult.Failure!,
                        });
                }

                return PhaseExecutionTrace.Success(
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    steps: planPassResult.CompiledSteps,
                    operationTraces: planPassResult.OperationTraces,
                    planToken: issueResult.PlanToken);
            }

            var validationResult = planTokenCoordinator.ValidateCall(
                request,
                planPassResult.OperationTraces,
                planPassResult.CompiledDigestPayloadUtf8,
                cancellationToken);
            if (!validationResult.IsSuccess)
            {
                return PhaseExecutionTrace.Failure(
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    steps: planPassResult.CompiledSteps,
                    operationTraces: planPassResult.OperationTraces,
                    errors: new[]
                    {
                        validationResult.Failure!,
                    });
            }

            if (!dangerousOperationCallAuthorizer.TryAuthorize(planPassResult.PreparedOperations, request.AllowDangerous, out var dangerousCallFailure))
            {
                return PhaseExecutionTrace.Failure(
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    steps: planPassResult.CompiledSteps,
                    operationTraces: planPassResult.OperationTraces,
                    errors: new[]
                    {
                        dangerousCallFailure!,
                    });
            }

            var callPassResult = await callPassExecutor.ExecuteAsync(planPassResult.PreparedOperations, executionContext, cancellationToken).ConfigureAwait(false);
            return callPassResult.IsSuccess
                ? PhaseExecutionTrace.Success(request.ProtocolVersion, request.RequestId, planPassResult.CompiledSteps, callPassResult.OperationTraces)
                : PhaseExecutionTrace.Failure(request.ProtocolVersion, request.RequestId, planPassResult.CompiledSteps, callPassResult.OperationTraces, callPassResult.Errors);
        }

        private static Func<NormalizedOperation, IUcliOperation, OperationFailure?> CreateDangerousCallPreflight ()
        {
            return static (operation, phaseOperation) =>
            {
                return phaseOperation.Metadata.Policy == OperationPolicy.Dangerous
                    ? DangerousOperationCallAuthorizer.CreateAllowDangerousFailure(operation)
                    : null;
            };
        }

        private static IReadOnlyList<NormalizedRequestStep> CreateUncompiledSteps (IReadOnlyList<IpcRequestContractStep> sourceSteps)
        {
            var steps = new NormalizedRequestStep[sourceSteps.Count];
            for (var i = 0; i < sourceSteps.Count; i++)
            {
                var sourceStep = sourceSteps[i];
                var kind = sourceStep.Kind ?? IpcRequestStepKind.Op;
                steps[i] = new NormalizedRequestStep(
                    Id: sourceStep.Id ?? string.Empty,
                    Kind: kind,
                    OperationName: kind == IpcRequestStepKind.Edit
                        ? "edit"
                        : sourceStep.OperationName ?? string.Empty,
                    PrimitiveCount: 0);
            }

            return steps;
        }

        private static PhaseExecutionTrace CreatePlanTokenValidationFailure (
            NormalizedExecuteRequest request,
            PlanPassResult planPassResult,
            OperationFailure validationFailure)
        {
            var remappedTraces = new OperationPhaseTrace[planPassResult.OperationTraces.Count];
            for (var i = 0; i < planPassResult.OperationTraces.Count; i++)
            {
                var trace = planPassResult.OperationTraces[i];
                if (trace.Failure == null)
                {
                    remappedTraces[i] = trace;
                    continue;
                }

                remappedTraces[i] = trace with
                {
                    Failure = CreatePlanTokenValidationFailure(trace.Failure, validationFailure),
                };
            }

            var remappedErrors = new OperationFailure[planPassResult.Errors.Count];
            for (var i = 0; i < planPassResult.Errors.Count; i++)
            {
                remappedErrors[i] = CreatePlanTokenValidationFailure(planPassResult.Errors[i], validationFailure);
            }

            return PhaseExecutionTrace.Failure(
                protocolVersion: request.ProtocolVersion,
                requestId: request.RequestId,
                steps: planPassResult.CompiledSteps,
                operationTraces: remappedTraces,
                errors: remappedErrors);
        }

        private static OperationFailure CreatePlanTokenValidationFailure (
            OperationFailure failure,
            OperationFailure validationFailure)
        {
            return new OperationFailure(
                Code: validationFailure.Code,
                Message: validationFailure.Message,
                OpId: failure.OpId ?? validationFailure.OpId);
        }
    }
}
