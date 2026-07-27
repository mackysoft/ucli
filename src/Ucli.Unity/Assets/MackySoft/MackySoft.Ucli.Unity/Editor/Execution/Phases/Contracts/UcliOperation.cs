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
        public Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadArgs(operation, out var args, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return ValidateAsync(operation, args!, executionContext, cancellationToken);
        }

        /// <inheritdoc />
        public Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadArgs(operation, out var args, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return PlanAsync(operation, args!, executionContext, cancellationToken);
        }

        /// <inheritdoc />
        public Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadArgs(operation, out var args, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return CallAsync(operation, args!, executionContext, cancellationToken);
        }

        /// <summary> Executes the validate phase with typed args. </summary>
        protected abstract Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            TArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken);

        /// <summary> Executes the plan phase with typed args. </summary>
        protected abstract Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            TArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken);

        /// <summary> Executes the call phase with typed args. </summary>
        protected abstract Task<OperationPhaseStepResult> CallAsync (
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
                SerializeResultToElement(result));
        }

        /// <summary> Serializes a typed result through its registered non-null object contract. </summary>
        protected static System.Text.Json.JsonElement SerializeResultToElement (TResult result)
        {
            return IpcPayloadCodec.SerializePublicRawOperationResultToElement(result);
        }

        private static bool TryReadArgs (
            NormalizedOperation operation,
            out TArgs? args,
            out OperationPhaseStepResult? failure)
        {
            args = default;
            failure = null;
            bool isDeserialized;
            TArgs value;
            IpcPayloadReadError error;
            if (operation.AllowRequestLocalAliases)
            {
                isDeserialized = IpcPayloadCodec.TryDeserializeStrictOperationArgs(operation.Args, out value, out error);
            }
            else
            {
                isDeserialized = IpcPayloadCodec.TryDeserializePublicRawOperationArgs(operation.Args, out value, out error);
            }

            if (!isDeserialized)
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Operation args do not match '{typeof(TArgs).Name}'. {error.Message}");
                return false;
            }

            args = value;
            return true;
        }
    }
}
