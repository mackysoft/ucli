using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Index;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.comp.schema</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class CompSchemaOperation : IUcliOperation
    {
        private const string ArgsSchemaJson =
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""properties"": {
                ""type"": { ""type"": ""string"", ""minLength"": 1 }
              },
              ""required"": [""type""]
            }";

        private readonly ComponentSchemaExtractor schemaExtractor =
            new ComponentSchemaExtractor(new IndexSchemaPropertyCollector());

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: "ucli.comp.schema",
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        public async Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Execute(operation, applied: false, cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Execute(operation, applied: true, cancellationToken).ConfigureAwait(false);
        }

        private async Task<OperationPhaseStepResult> Execute (
            NormalizedOperation operation,
            bool applied,
            CancellationToken cancellationToken)
        {
            if (!TryValidateArguments(operation, out var componentType, out var failure))
            {
                return failure!;
            }

            var extractionResult = await schemaExtractor.Extract(
                new[] { componentType! },
                cancellationToken).ConfigureAwait(false);
            if (extractionResult.Entries.Count == 0)
            {
                return OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: $"Schema could not be extracted for type '{componentType!.FullName}'.",
                    OpId: operation.Id));
            }

            return OperationPhaseStepResult.Success(
                applied: applied,
                changed: false,
                result: IpcPayloadCodec.SerializeToElement(extractionResult.Entries[0]));
        }

        private static bool TryValidateArguments (
            NormalizedOperation operation,
            out System.Type? componentType,
            out OperationPhaseStepResult? failure)
        {
            componentType = null;
            failure = null;
            if (!CompSchemaArgumentsCodec.TryParse(operation.Args, out var parsedArguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!ComponentTypeResolver.TryResolveComponentType(parsedArguments.TypeId, out componentType, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            return true;
        }
    }
}
