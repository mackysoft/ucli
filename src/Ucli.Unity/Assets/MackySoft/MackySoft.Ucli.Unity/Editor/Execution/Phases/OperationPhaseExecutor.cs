using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Executes normalized operations through <c>validate -&gt; plan -&gt; call</c> phase pipelines. </summary>
    internal sealed class OperationPhaseExecutor : IOperationPhaseExecutor
    {
        private readonly IPhaseOperationRegistry operationRegistry;

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
        {
            this.operationRegistry = operationRegistry ?? throw new ArgumentNullException(nameof(operationRegistry));
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

            var planPassResult = await ExecutePlanPass(request, cancellationToken).ConfigureAwait(false);
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

            var callPassResult = await ExecuteCallPass(planPassResult.PreparedOperations, cancellationToken).ConfigureAwait(false);
            return callPassResult.IsSuccess
                ? PhaseExecutionTrace.Success(request.ProtocolVersion, request.RequestId, callPassResult.OperationTraces)
                : PhaseExecutionTrace.Failure(request.ProtocolVersion, request.RequestId, callPassResult.OperationTraces, callPassResult.Errors);
        }

        /// <summary> Executes validate and plan phases for all operations with fail-fast semantics. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The plan-pass result. </returns>
        private async Task<PlanPassResult> ExecutePlanPass (
            NormalizedExecuteRequest request,
            CancellationToken cancellationToken)
        {
            var operationTraces = new List<OperationPhaseTrace>(request.Ops.Count);
            var errors = new List<OperationFailure>(1);
            var preparedOperations = new List<PreparedOperation>(request.Ops.Count);
            var operationUseCounts = CountOperationUse(request.Ops);
            var hasFailed = false;

            for (var i = 0; i < request.Ops.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var operation = request.Ops[i];
                if (hasFailed)
                {
                    operationTraces.Add(CreateSkippedTrace(operation));
                    continue;
                }

                if (!operationRegistry.TryResolve(operation.Op, out var phaseOperation))
                {
                    var missingOperationFailure = new OperationFailure(
                        Code: IpcErrorCodes.CommandNotImplemented,
                        Message: $"Operation '{operation.Op}' is not implemented.",
                        OpId: operation.Id);
                    operationTraces.Add(new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Validate,
                        Applied: false,
                        Changed: false,
                        Touched: Array.Empty<OperationTouch>(),
                        Failure: missingOperationFailure));
                    errors.Add(missingOperationFailure);
                    hasFailed = true;
                    continue;
                }

                var touched = new List<OperationTouch>();
                var validateStepResult = await ExecutePhaseStep(
                    operation,
                    OperationPhase.Validate,
                    ct => phaseOperation.Validate(operation, ct),
                    cancellationToken).ConfigureAwait(false);
                MergeTouched(touched, validateStepResult.Touched);
                if (!validateStepResult.IsSuccess)
                {
                    var touchedSnapshot = touched.ToArray();
                    operationTraces.Add(new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Validate,
                        Applied: validateStepResult.Applied,
                        Changed: validateStepResult.Changed,
                        Touched: touchedSnapshot,
                        Failure: validateStepResult.Failure));
                    errors.Add(validateStepResult.Failure!);
                    hasFailed = true;
                    continue;
                }

                var planStepResult = await ExecutePhaseStep(
                    operation,
                    OperationPhase.Plan,
                    ct => phaseOperation.Plan(operation, ct),
                    cancellationToken).ConfigureAwait(false);
                MergeTouched(touched, planStepResult.Touched);
                if (!planStepResult.IsSuccess)
                {
                    var touchedSnapshot = touched.ToArray();
                    operationTraces.Add(new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Plan,
                        Applied: planStepResult.Applied,
                        Changed: planStepResult.Changed,
                        Touched: touchedSnapshot,
                        Failure: planStepResult.Failure));
                    errors.Add(planStepResult.Failure!);
                    hasFailed = true;
                    continue;
                }

                var successfulTouched = touched.ToArray();
                operationTraces.Add(new OperationPhaseTrace(
                    OpId: operation.Id,
                    Op: operation.Op,
                    Phase: OperationPhase.Plan,
                    Applied: planStepResult.Applied,
                    Changed: planStepResult.Changed,
                    Touched: successfulTouched,
                    Failure: null));
                preparedOperations.Add(new PreparedOperation(
                    Operation: operation,
                    PhaseOperation: phaseOperation,
                    PlanTouched: successfulTouched,
                    RequiresPreCallPlanReplay: operationUseCounts[operation.Op] > 1));
            }

            return new PlanPassResult(
                OperationTraces: operationTraces,
                Errors: errors,
                PreparedOperations: preparedOperations);
        }

        /// <summary> Executes call phase for prevalidated and preplanned operations. </summary>
        /// <param name="preparedOperations"> The prepared operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The call-pass result. </returns>
        private static async Task<CallPassResult> ExecuteCallPass (
            IReadOnlyList<PreparedOperation> preparedOperations,
            CancellationToken cancellationToken)
        {
            var operationTraces = new List<OperationPhaseTrace>(preparedOperations.Count);
            var errors = new List<OperationFailure>(1);
            var hasFailed = false;

            for (var i = 0; i < preparedOperations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var preparedOperation = preparedOperations[i];
                if (hasFailed)
                {
                    operationTraces.Add(CreateSkippedTrace(preparedOperation.Operation));
                    continue;
                }

                var touched = new List<OperationTouch>(preparedOperation.PlanTouched.Count);
                MergeTouched(touched, preparedOperation.PlanTouched);

                if (preparedOperation.RequiresPreCallPlanReplay)
                {
                    // NOTE:
                    // Duplicate operation names resolve to the same phase-operation instance.
                    // Replaying Plan immediately before Call keeps per-op planned state adjacent.
                    var replayedPlanStepResult = await ExecutePhaseStep(
                        preparedOperation.Operation,
                        OperationPhase.Plan,
                        ct => preparedOperation.PhaseOperation.Plan(preparedOperation.Operation, ct),
                        cancellationToken).ConfigureAwait(false);
                    MergeTouched(touched, replayedPlanStepResult.Touched);

                    if (!replayedPlanStepResult.IsSuccess)
                    {
                        var replayTouchedSnapshot = touched.ToArray();
                        operationTraces.Add(new OperationPhaseTrace(
                            OpId: preparedOperation.Operation.Id,
                            Op: preparedOperation.Operation.Op,
                            Phase: OperationPhase.Plan,
                            Applied: replayedPlanStepResult.Applied,
                            Changed: replayedPlanStepResult.Changed,
                            Touched: replayTouchedSnapshot,
                            Failure: replayedPlanStepResult.Failure));
                        errors.Add(replayedPlanStepResult.Failure!);
                        hasFailed = true;
                        continue;
                    }
                }

                var callStepResult = await ExecutePhaseStep(
                    preparedOperation.Operation,
                    OperationPhase.Call,
                    ct => preparedOperation.PhaseOperation.Call(preparedOperation.Operation, ct),
                    cancellationToken).ConfigureAwait(false);

                MergeTouched(touched, callStepResult.Touched);
                var touchedSnapshot = touched.ToArray();

                if (!callStepResult.IsSuccess)
                {
                    operationTraces.Add(new OperationPhaseTrace(
                        OpId: preparedOperation.Operation.Id,
                        Op: preparedOperation.Operation.Op,
                        Phase: OperationPhase.Call,
                        Applied: callStepResult.Applied,
                        Changed: callStepResult.Changed,
                        Touched: touchedSnapshot,
                        Failure: callStepResult.Failure));
                    errors.Add(callStepResult.Failure!);
                    hasFailed = true;
                    continue;
                }

                operationTraces.Add(new OperationPhaseTrace(
                    OpId: preparedOperation.Operation.Id,
                    Op: preparedOperation.Operation.Op,
                    Phase: OperationPhase.Call,
                    Applied: callStepResult.Applied,
                    Changed: callStepResult.Changed,
                    Touched: touchedSnapshot,
                    Failure: null));
            }

            return new CallPassResult(
                OperationTraces: operationTraces,
                Errors: errors);
        }

        /// <summary> Counts operation-name usage in one request. </summary>
        /// <param name="operations"> The normalized operations. </param>
        /// <returns> The usage count per operation name. </returns>
        private static Dictionary<string, int> CountOperationUse (IReadOnlyList<NormalizedOperation> operations)
        {
            var useCounts = new Dictionary<string, int>(operations.Count, StringComparer.Ordinal);
            for (var i = 0; i < operations.Count; i++)
            {
                var operationName = operations[i].Op;
                if (useCounts.TryGetValue(operationName, out var count))
                {
                    useCounts[operationName] = count + 1;
                }
                else
                {
                    useCounts.Add(operationName, 1);
                }
            }

            return useCounts;
        }

        /// <summary> Executes one phase step with exception-to-failure translation. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="phase"> The phase being executed. </param>
        /// <param name="executor"> The step executor delegate. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        private static async Task<OperationPhaseStepResult> ExecutePhaseStep (
            NormalizedOperation operation,
            OperationPhase phase,
            Func<CancellationToken, Task<OperationPhaseStepResult>> executor,
            CancellationToken cancellationToken)
        {
            try
            {
                var stepResult = await executor(cancellationToken).ConfigureAwait(false);
                if (stepResult == null)
                {
                    return OperationPhaseStepResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.InternalError,
                        Message: $"Operation '{operation.Id}' returned null result at phase '{phase}'.",
                        OpId: operation.Id));
                }

                return stepResult;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: $"Unexpected error occurred in operation '{operation.Id}' at phase '{phase}'. {exception.Message}",
                    OpId: operation.Id));
            }
        }

        /// <summary> Merges touched entries into one target list. </summary>
        /// <param name="target"> The target touched-entry list. </param>
        /// <param name="source"> The source touched-entry collection. </param>
        private static void MergeTouched (
            List<OperationTouch> target,
            IReadOnlyList<OperationTouch> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        /// <summary> Creates a skipped trace for operations after fail-fast stopping. </summary>
        /// <param name="operation"> The skipped operation. </param>
        /// <returns> The skipped trace entry. </returns>
        private static OperationPhaseTrace CreateSkippedTrace (NormalizedOperation operation)
        {
            return new OperationPhaseTrace(
                OpId: operation.Id,
                Op: operation.Op,
                Phase: OperationPhase.Skipped,
                Applied: false,
                Changed: false,
                Touched: Array.Empty<OperationTouch>(),
                Failure: null);
        }

        /// <summary> Represents one preplanned operation prepared by validate/plan pass. </summary>
        /// <param name="Operation"> The normalized operation model. </param>
        /// <param name="PhaseOperation"> The resolved phase operation implementation. </param>
        /// <param name="PlanTouched"> The touched list produced by validate and plan phases. </param>
        /// <param name="RequiresPreCallPlanReplay"> Whether plan should be replayed immediately before call. </param>
        private sealed record PreparedOperation (
            NormalizedOperation Operation,
            IPhaseOperation PhaseOperation,
            IReadOnlyList<OperationTouch> PlanTouched,
            bool RequiresPreCallPlanReplay);

        /// <summary> Represents one validate/plan pass result. </summary>
        /// <param name="OperationTraces"> The per-operation traces from validate/plan pass. </param>
        /// <param name="Errors"> The validate/plan pass errors. </param>
        /// <param name="PreparedOperations"> The operations prepared for call-phase execution. </param>
        private sealed record PlanPassResult (
            IReadOnlyList<OperationPhaseTrace> OperationTraces,
            IReadOnlyList<OperationFailure> Errors,
            IReadOnlyList<PreparedOperation> PreparedOperations)
        {
            /// <summary> Gets a value indicating whether validate/plan pass succeeded. </summary>
            public bool IsSuccess => Errors.Count == 0;
        }

        /// <summary> Represents one call-pass result. </summary>
        /// <param name="OperationTraces"> The per-operation traces from call pass. </param>
        /// <param name="Errors"> The call-pass errors. </param>
        private sealed record CallPassResult (
            IReadOnlyList<OperationPhaseTrace> OperationTraces,
            IReadOnlyList<OperationFailure> Errors)
        {
            /// <summary> Gets a value indicating whether call pass succeeded. </summary>
            public bool IsSuccess => Errors.Count == 0;
        }
    }
}
