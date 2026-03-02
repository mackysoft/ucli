using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements selector resolution flow for the <c>ucli.resolve</c> operation. </summary>
    internal sealed class ResolvePhaseOperation : IPhaseOperation
    {
        /// <summary> Gets the operation name served by this implementation. </summary>
        public string OperationName => "ucli.resolve";

        /// <summary> Executes validate phase for <c>ucli.resolve</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operation" /> or <paramref name="executionContext" /> is <see langword="null" />. </exception>
        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureArguments(operation, executionContext);

            if (!ResolveSelectorCodec.TryParse(operation.Args, out var selector, out var errorMessage))
            {
                return Task.FromResult(CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            if (selector.Kind == ResolveSelectorKind.GlobalObjectId
                && !ResolveReferenceResolver.IsValidGlobalObjectIdText(selector.GlobalObjectId!))
            {
                return Task.FromResult(CreateInvalidArgumentFailure(
                    operation.Id,
                    $"'{ResolveSelectorPropertyNames.GlobalObjectId}' must be a valid GlobalObjectId string."));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.resolve</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operation" /> or <paramref name="executionContext" /> is <see langword="null" />. </exception>
        public Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            return ExecuteResolve(operation, executionContext, applied: false, cancellationToken);
        }

        /// <summary> Executes call phase for <c>ucli.resolve</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operation" /> or <paramref name="executionContext" /> is <see langword="null" />. </exception>
        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            return ExecuteResolve(operation, executionContext, applied: true, cancellationToken);
        }

        /// <summary> Executes selector parse/resolve flow shared by plan and call phases. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="applied"> The applied flag for successful phase result. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        private static Task<OperationPhaseStepResult> ExecuteResolve (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool applied,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureArguments(operation, executionContext);

            if (!ResolveSelectorCodec.TryParse(operation.Args, out var selector, out var parseErrorMessage))
            {
                return Task.FromResult(CreateInvalidArgumentFailure(operation.Id, parseErrorMessage));
            }

            if (!ResolveReferenceResolver.TryResolve(selector, out var resolvedReference, out var resolveErrorMessage))
            {
                return Task.FromResult(CreateInvalidArgumentFailure(operation.Id, resolveErrorMessage));
            }

            StoreAliasIfNeeded(operation.As, executionContext, resolvedReference!);
            return Task.FromResult(OperationPhaseStepResult.Success(applied, changed: false));
        }

        /// <summary> Stores one resolved reference to alias store when alias is specified. </summary>
        /// <param name="alias"> The operation alias. </param>
        /// <param name="executionContext"> The execution context that owns the alias store. </param>
        /// <param name="resolvedReference"> The resolved reference value. </param>
        private static void StoreAliasIfNeeded (
            string? alias,
            OperationExecutionContext executionContext,
            ResolvedReference resolvedReference)
        {
            if (StringValueNormalizer.TrimToNull(alias) == null)
            {
                return;
            }

            executionContext.AliasStore.Set(alias!, resolvedReference);
        }

        /// <summary> Throws for null operation/context arguments. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context. </param>
        /// <exception cref="ArgumentNullException"> Thrown when an argument is <see langword="null" />. </exception>
        private static void EnsureArguments (
            NormalizedOperation operation,
            OperationExecutionContext executionContext)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }
        }

        /// <summary> Creates a standardized INVALID_ARGUMENT failure result for one operation. </summary>
        /// <param name="operationId"> The operation identifier. </param>
        /// <param name="message"> The error message. </param>
        /// <returns> The failed phase-step result. </returns>
        private static OperationPhaseStepResult CreateInvalidArgumentFailure (
            string operationId,
            string message)
        {
            return OperationPhaseStepResult.Failed(new OperationFailure(
                Code: IpcErrorCodes.InvalidArgument,
                Message: message,
                OpId: operationId));
        }
    }
}
