using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Operations;
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
        public async Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadArgs(operation, out var args, out var failure))
            {
                return failure!;
            }

            var stepResult = await ValidateAsync(
                operation,
                args!,
                executionContext,
                cancellationToken);
            return CompleteNonCallTypedResult(stepResult);
        }

        /// <inheritdoc />
        public async Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadArgs(operation, out var args, out var failure))
            {
                return failure!;
            }

            var stepResult = await PlanAsync(
                operation,
                args!,
                executionContext,
                cancellationToken);
            return CompleteNonCallTypedResult(stepResult);
        }

        /// <inheritdoc />
        public async Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadArgs(operation, out var args, out var failure))
            {
                return failure!;
            }

            var stepResult = await CallAsync(
                operation,
                args!,
                executionContext,
                cancellationToken);
            return CompleteCallTypedResult(stepResult);
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
            IReadOnlyList<OperationTouch> touched)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (touched == null)
            {
                throw new ArgumentNullException(nameof(touched));
            }

            return OperationPhaseStepResult.SuccessWithTypedResult(
                result,
                applied,
                changed,
                touched);
        }

        private static OperationPhaseStepResult CompleteNonCallTypedResult (
            OperationPhaseStepResult stepResult)
        {
            if (stepResult.TypedResult == null)
            {
                return stepResult;
            }

            if (typeof(TResult) == typeof(UcliNoResult))
            {
                throw new InvalidOperationException(
                    "An operation that declares UcliNoResult must not return a typed result.");
            }

            if (stepResult.TypedResult is not TResult typedResult)
            {
                throw new InvalidOperationException(
                    "The operation returned a typed result that does not match its declared TResult.");
            }

            var serializedResult = IpcPayloadCodec.SerializePublicRawOperationResultToElement(
                typedResult);
            return stepResult with
            {
                Result = OperationPhaseStepResult.CloneResult(serializedResult),
                Verdict = null,
                TypedResult = null,
            };
        }

        private OperationPhaseStepResult CompleteCallTypedResult (
            OperationPhaseStepResult stepResult)
        {
            if (stepResult.TypedResult == null)
            {
                if (stepResult.IsSuccess
                    && typeof(TResult) != typeof(UcliNoResult))
                {
                    throw new InvalidOperationException(
                        "A successful Call must return the operation's declared TResult.");
                }

                return stepResult;
            }

            if (typeof(TResult) == typeof(UcliNoResult))
            {
                throw new InvalidOperationException(
                    "An operation that declares UcliNoResult must not return a typed result.");
            }

            if (stepResult.TypedResult is not TResult typedResult)
            {
                throw new InvalidOperationException(
                    "The operation returned a typed result that does not match its declared TResult.");
            }

            return Metadata.CompleteCallResult(stepResult);
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
