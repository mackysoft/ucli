using System;
using MackySoft.Ucli.Contracts;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Index;
using MackySoft.Ucli.Contracts.Operations;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.comp.schema</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class CompSchemaOperation : UcliOperation<ComponentTypeArgs, IndexSchemaEntryJsonContract>
    {
        private readonly ComponentSchemaExtractor schemaExtractor =
            new ComponentSchemaExtractor(new IndexSchemaPropertyCollector());

        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<ComponentTypeArgs, IndexSchemaEntryJsonContract>(
            operationName: UcliPrimitiveOperationNames.CompSchema,
            kind: UcliOperationKind.Query,
            description: "Returns the serialized schema for a component type.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.ObservesUnityState },
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate the component type and observe serialized property metadata without applying mutation.",
                callSemantics: "Read serialized schema metadata for the requested component type without applying mutation.",
                touchedContract: "Returns no touched resources because schema metadata is observational data.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Timeout, cancellation, or schema extraction failure means the schema was not fully produced.",
                dangerousNotes: Array.Empty<string>()));

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            ComponentTypeArgs args,
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

        protected override async Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            ComponentTypeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await ExecuteAsync(operation, args, cancellationToken);
        }

        protected override async Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            ComponentTypeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await ExecuteAsync(operation, args, cancellationToken);
        }

        private async Task<OperationPhaseStepResult> ExecuteAsync (
            NormalizedOperation operation,
            ComponentTypeArgs args,
            CancellationToken cancellationToken)
        {
            if (!TryValidateArguments(operation, args, out var validationState, out var failure))
            {
                return failure!;
            }

            var extractionResult = await schemaExtractor.ExtractAsync(
                new[] { validationState.ComponentType! },
                cancellationToken);
            if (extractionResult.Entries.Count == 0)
            {
                return OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: UcliCoreErrorCodes.InternalError,
                    Message: $"Schema could not be extracted for type '{validationState.ComponentType!.FullName}'.",
                    OpId: operation.Id));
            }

            return OperationPhaseStepResult.Success(
                applied: false,
                changed: false,
                result: IpcPayloadCodec.SerializeToElement(extractionResult.Entries[0]));
        }

        private static bool TryValidateArguments (
            NormalizedOperation operation,
            ComponentTypeArgs args,
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
