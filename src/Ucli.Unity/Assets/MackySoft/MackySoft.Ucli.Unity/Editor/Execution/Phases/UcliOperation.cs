using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Base class for operations that keep JSON parsing at the IPC boundary and run phases with typed args. </summary>
    /// <typeparam name="TArgs"> The operation args contract type. </typeparam>
    /// <typeparam name="TResult"> The operation result contract type. </typeparam>
    public abstract class UcliOperation<TArgs, TResult> : IUcliOperation<TArgs, TResult>
    {
        /// <inheritdoc />
        public abstract UcliOperationMetadata Metadata { get; }

        /// <inheritdoc />
        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadArgs(operation, out var args, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Validate(operation, args!, executionContext, cancellationToken);
        }

        /// <inheritdoc />
        public Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadArgs(operation, out var args, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Plan(operation, args!, executionContext, cancellationToken);
        }

        /// <inheritdoc />
        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadArgs(operation, out var args, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Call(operation, args!, executionContext, cancellationToken);
        }

        /// <summary> Executes the validate phase with typed args. </summary>
        protected abstract Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            TArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken);

        /// <summary> Executes the plan phase with typed args. </summary>
        protected abstract Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            TArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken);

        /// <summary> Executes the call phase with typed args. </summary>
        protected abstract Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            TArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken);

        /// <summary> Serializes a typed result payload into a successful phase result. </summary>
        protected static OperationPhaseStepResult SuccessWithResult (
            TResult result,
            bool applied,
            bool changed,
            IReadOnlyList<OperationTouch>? touched = null)
        {
            return OperationPhaseStepResult.Success(
                applied,
                changed,
                touched,
                IpcPayloadCodec.SerializeToElement(result));
        }

        private static bool TryReadArgs (
            NormalizedOperation operation,
            out TArgs? args,
            out OperationPhaseStepResult? failure)
        {
            args = default;
            failure = null;
            if (!IpcPayloadCodec.TryDeserializeStrict(operation.Args, out TArgs value, out var error))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Operation args do not match '{typeof(TArgs).Name}'. {error.Message}");
                return false;
            }

            if (!UcliOperationContractValidator.TryValidate(value, typeof(TArgs), out var validationError))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, validationError);
                return false;
            }

            args = value;
            return true;
        }
    }
}
