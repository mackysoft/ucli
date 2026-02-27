using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Executes normalized operations through <c>validate -&gt; plan -&gt; call</c> phase pipelines. </summary>
    internal sealed class OperationPhaseExecutor : IOperationPhaseExecutor
    {
        private readonly IPhaseOperationRegistry operationRegistry;

        /// <summary> Initializes a new instance of the <see cref="OperationPhaseExecutor" /> class. </summary>
        /// <param name="operationRegistry"> The phase-operation registry dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationRegistry" /> is <see langword="null" />. </exception>
        public OperationPhaseExecutor (IPhaseOperationRegistry operationRegistry)
        {
            this.operationRegistry = operationRegistry ?? throw new ArgumentNullException(nameof(operationRegistry));
        }

        /// <summary> Executes one normalized request through the specified command phase-flow. </summary>
        /// <param name="command"> The top-level execution command. </param>
        /// <param name="request"> The normalized request. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The request-level execution trace. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
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

            var operationTraces = new List<OperationPhaseTrace>(request.Ops.Count);
            var errors = new List<OperationFailure>(1);
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

                var operationExecutionResult = await ExecuteOperation(command, operation, phaseOperation, cancellationToken);
                operationTraces.Add(operationExecutionResult.Trace);
                if (operationExecutionResult.Failure is not null)
                {
                    errors.Add(operationExecutionResult.Failure);
                    hasFailed = true;
                }
            }

            return errors.Count == 0
                ? PhaseExecutionTrace.Success(request.ProtocolVersion, request.RequestId, operationTraces)
                : PhaseExecutionTrace.Failure(request.ProtocolVersion, request.RequestId, operationTraces, errors);
        }

        /// <summary> Executes one operation according to the top-level command phase-flow. </summary>
        /// <param name="command"> The top-level execution command. </param>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="phaseOperation"> The concrete operation implementation. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The operation execution result. </returns>
        private static async Task<OperationExecutionResult> ExecuteOperation (
            PhaseExecutionCommand command,
            NormalizedOperation operation,
            IPhaseOperation phaseOperation,
            CancellationToken cancellationToken)
        {
            var touched = new List<OperationTouch>();

            var validateStepResult = await ExecutePhaseStep(
                operation,
                OperationPhase.Validate,
                ct => phaseOperation.Validate(operation, ct),
                cancellationToken);
            MergeTouched(touched, validateStepResult.Touched);
            if (!validateStepResult.IsSuccess)
            {
                return OperationExecutionResult.Failed(new OperationPhaseTrace(
                    OpId: operation.Id,
                    Op: operation.Op,
                    Phase: OperationPhase.Validate,
                    Applied: validateStepResult.Applied,
                    Changed: validateStepResult.Changed,
                    Touched: touched,
                    Failure: validateStepResult.Failure),
                    validateStepResult.Failure!);
            }

            var planStepResult = await ExecutePhaseStep(
                operation,
                OperationPhase.Plan,
                ct => phaseOperation.Plan(operation, ct),
                cancellationToken);
            MergeTouched(touched, planStepResult.Touched);
            if (!planStepResult.IsSuccess)
            {
                return OperationExecutionResult.Failed(new OperationPhaseTrace(
                    OpId: operation.Id,
                    Op: operation.Op,
                    Phase: OperationPhase.Plan,
                    Applied: planStepResult.Applied,
                    Changed: planStepResult.Changed,
                    Touched: touched,
                    Failure: planStepResult.Failure),
                    planStepResult.Failure!);
            }

            if (command == PhaseExecutionCommand.Plan)
            {
                return OperationExecutionResult.Succeeded(new OperationPhaseTrace(
                    OpId: operation.Id,
                    Op: operation.Op,
                    Phase: OperationPhase.Plan,
                    Applied: planStepResult.Applied,
                    Changed: planStepResult.Changed,
                    Touched: touched,
                    Failure: null));
            }

            var callStepResult = await ExecutePhaseStep(
                operation,
                OperationPhase.Call,
                ct => phaseOperation.Call(operation, ct),
                cancellationToken);
            MergeTouched(touched, callStepResult.Touched);
            if (!callStepResult.IsSuccess)
            {
                return OperationExecutionResult.Failed(new OperationPhaseTrace(
                    OpId: operation.Id,
                    Op: operation.Op,
                    Phase: OperationPhase.Call,
                    Applied: callStepResult.Applied,
                    Changed: callStepResult.Changed,
                    Touched: touched,
                    Failure: callStepResult.Failure),
                    callStepResult.Failure!);
            }

            return OperationExecutionResult.Succeeded(new OperationPhaseTrace(
                OpId: operation.Id,
                Op: operation.Op,
                Phase: OperationPhase.Call,
                Applied: callStepResult.Applied,
                Changed: callStepResult.Changed,
                Touched: touched,
                Failure: null));
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
                var stepResult = await executor(cancellationToken);
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

        /// <summary> Represents one operation execution result. </summary>
        /// <param name="Trace"> The operation trace entry. </param>
        /// <param name="Failure"> The operation failure details; otherwise <see langword="null" />. </param>
        private sealed record OperationExecutionResult (
            OperationPhaseTrace Trace,
            OperationFailure? Failure)
        {
            /// <summary> Creates a successful operation execution result. </summary>
            /// <param name="trace"> The operation trace entry. </param>
            /// <returns> The successful operation execution result. </returns>
            public static OperationExecutionResult Succeeded (OperationPhaseTrace trace)
            {
                return new OperationExecutionResult(trace, null);
            }

            /// <summary> Creates a failed operation execution result. </summary>
            /// <param name="trace"> The operation trace entry. </param>
            /// <param name="failure"> The operation failure details. </param>
            /// <returns> The failed operation execution result. </returns>
            public static OperationExecutionResult Failed (
                OperationPhaseTrace trace,
                OperationFailure failure)
            {
                return new OperationExecutionResult(trace, failure);
            }
        }
    }
}
