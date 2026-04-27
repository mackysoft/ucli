using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements selector resolution flow for the <c>ucli.resolve</c> operation. </summary>
    [UcliOperation]
    internal sealed class ResolveOperation : IUcliOperation
    {
        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: UcliPrimitiveOperationNames.Resolve,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            argsSchemaJson: IpcResolveSelectorArgsSchema.Json);

        /// <summary> Executes validate phase for <c>ucli.resolve</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            if (!ResolveSelectorCodec.TryParse(operation.Args, out var selector, out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            if (selector.Kind == ResolveSelectorKind.GlobalObjectId
                && !ResolveReferenceResolver.IsValidGlobalObjectIdText(selector.GlobalObjectId!))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"'{IpcResolveSelectorPropertyNames.GlobalObjectId}' must be a valid GlobalObjectId string."));
            }

            if (!TryValidateSupportedSelector(selector, operation.Id, out var unsupportedSelectorResult))
            {
                return Task.FromResult(unsupportedSelectorResult!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.resolve</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            return ExecuteResolve(operation, executionContext, applied: false);
        }

        /// <summary> Executes call phase for <c>ucli.resolve</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            return ExecuteResolve(operation, executionContext, applied: true);
        }

        /// <summary> Executes selector parse/resolve flow shared by plan and call phases. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="applied"> The applied flag for successful phase result. </param>
        /// <returns> The phase-step result. </returns>
        private static Task<OperationPhaseStepResult> ExecuteResolve (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool applied)
        {
            if (!ResolveSelectorCodec.TryParse(operation.Args, out var selector, out var parseErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parseErrorMessage));
            }

            if (!TryValidateSupportedSelector(selector, operation.Id, out var unsupportedSelectorResult))
            {
                return Task.FromResult(unsupportedSelectorResult!);
            }

            if (!ResolveReferenceResolver.TryResolveStableReference(selector, executionContext, allowTemporaryState: !applied, out var resolvedReference, out var resolveErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, resolveErrorMessage));
            }

            StoreAliasIfNeeded(operation.As, executionContext, resolvedReference!);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied,
                changed: false,
                result: IpcPayloadCodec.SerializeToElement(new IpcResolveOperationResult(resolvedReference!.GlobalObjectId))));
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
            if (alias == null)
            {
                return;
            }

            executionContext.AliasStore.Set(alias, resolvedReference);
        }

        private static bool TryValidateSupportedSelector (
            ResolveSelector selector,
            string operationId,
            out OperationPhaseStepResult? failure)
        {
            failure = null;
            if (selector.Kind != ResolveSelectorKind.PrefabComponent)
            {
                return true;
            }

            failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                operationId,
                "Operation 'ucli.resolve' does not support prefab component selectors.");
            return false;
        }
    }
}
