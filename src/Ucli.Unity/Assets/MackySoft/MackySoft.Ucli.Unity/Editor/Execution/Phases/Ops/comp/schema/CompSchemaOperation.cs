using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Index;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.comp.schema</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class CompSchemaOperation : TypedUcliOperation<UcliOperationContracts.TypeArgs, IndexSchemaEntryJsonContract>
    {
        private readonly ComponentSchemaExtractor schemaExtractor =
            new ComponentSchemaExtractor(new IndexSchemaPropertyCollector());

        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliOperationContracts.TypeArgs, IndexSchemaEntryJsonContract>(
            operationName: UcliPrimitiveOperationNames.CompSchema,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe);

        protected override Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            UcliOperationContracts.TypeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, args, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        protected override async Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            UcliOperationContracts.TypeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Execute(operation, args, applied: false, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            UcliOperationContracts.TypeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Execute(operation, args, applied: true, cancellationToken).ConfigureAwait(false);
        }

        private async Task<OperationPhaseStepResult> Execute (
            NormalizedOperation operation,
            UcliOperationContracts.TypeArgs args,
            bool applied,
            CancellationToken cancellationToken)
        {
            if (!TryValidateArguments(operation, args, out var validationState, out var failure))
            {
                return failure!;
            }

            var extractionResult = await schemaExtractor.Extract(
                new[] { validationState.ComponentType! },
                cancellationToken).ConfigureAwait(false);
            if (extractionResult.Entries.Count == 0)
            {
                return OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: $"Schema could not be extracted for type '{validationState.ComponentType!.FullName}'.",
                    OpId: operation.Id));
            }

            return OperationPhaseStepResult.Success(
                applied: applied,
                changed: false,
                result: IpcPayloadCodec.SerializeToElement(extractionResult.Entries[0]));
        }

        private static bool TryValidateArguments (
            NormalizedOperation operation,
            UcliOperationContracts.TypeArgs args,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;
            if (!ComponentTypeResolver.TryResolveComponentType(args.Type, out var componentType, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            validationState = new ValidationState(componentType);
            return true;
        }

        private readonly struct ValidationState
        {
            public ValidationState (Type componentType)
            {
                ComponentType = componentType;
            }

            public Type? ComponentType { get; }
        }
    }
}
