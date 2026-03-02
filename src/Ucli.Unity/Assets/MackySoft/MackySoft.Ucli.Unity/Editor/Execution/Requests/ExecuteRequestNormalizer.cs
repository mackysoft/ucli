using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Validates and normalizes execute request payloads into strict contract models. </summary>
    internal sealed class ExecuteRequestNormalizer : IExecuteRequestNormalizer
    {
        /// <summary> Validates and normalizes one execute request payload. </summary>
        /// <param name="request"> The execute request payload. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The normalization result that contains either normalized request data or one structured error. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        public ExecuteRequestNormalizationResult Normalize (
            IpcExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!IpcExecuteCommandNames.IsOperationPipelineCommand(request.Command))
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: $"Execute command is not supported: {request.Command}.",
                    opId: null));
            }

            if (request.Arguments.ValueKind != JsonValueKind.Object)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request arguments must be a JSON object.",
                    opId: null));
            }

            if (!IpcRequestContractReader.TryRead(
                requestObject: request.Arguments,
                profile: IpcRequestContractReadProfile.StrictExecute,
                requestContract: out var parsedContract,
                error: out var readError))
            {
                return ExecuteRequestNormalizationResult.Failure(MapReadError(readError));
            }

            if (parsedContract.ProtocolVersion != IpcProtocol.CurrentVersion)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.ProtocolVersionMismatch(
                    expectedVersion: IpcProtocol.CurrentVersion,
                    actualVersion: parsedContract.ProtocolVersion));
            }

            if (parsedContract.RequestId is null)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request property 'requestId' is required.",
                    opId: null));
            }

            if (parsedContract.Operations is null)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request property 'ops' is required.",
                    opId: null));
            }

            var normalizedOperations = new List<NormalizedOperation>(parsedContract.Operations.Count);
            foreach (var operation in parsedContract.Operations)
            {
                if (operation is null)
                {
                    return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                        message: "Operation must be an object.",
                        opId: null));
                }

                if (operation.Id is null)
                {
                    return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                        message: "Operation id is required.",
                        opId: null));
                }

                if (operation.Name is null)
                {
                    return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                        message: "Operation name is required.",
                        opId: operation.Id));
                }

                NormalizedExpectation? normalizedExpectation = null;
                if (operation.Expectation.HasValue)
                {
                    var expectation = operation.Expectation.Value;
                    normalizedExpectation = new NormalizedExpectation(
                        NonNull: expectation.NonNull,
                        Count: expectation.Count,
                        Min: expectation.Min,
                        Max: expectation.Max);
                }

                normalizedOperations.Add(new NormalizedOperation(
                    Id: operation.Id,
                    Op: operation.Name,
                    Args: operation.Args,
                    As: operation.Alias,
                    Expect: normalizedExpectation));
            }

            var canonicalPayload = CanonicalRequestWriter.WriteDigestPayload(parsedContract.ProtocolVersion, normalizedOperations);
            var normalizedPlanToken = StringValueNormalizer.TrimToNull(request.PlanToken);
            var normalizedRequest = new NormalizedExecuteRequest(
                ProtocolVersion: parsedContract.ProtocolVersion,
                RequestId: parsedContract.RequestId,
                Ops: normalizedOperations,
                PlanToken: normalizedPlanToken,
                CanonicalDigestPayloadUtf8: canonicalPayload);
            return ExecuteRequestNormalizationResult.Success(normalizedRequest);
        }

        private static ExecuteRequestNormalizationError MapReadError (in IpcRequestContractReadError readError)
        {
            var violation = IpcRequestContractViolationClassifier.Classify(readError);
            var operationId = violation.OperationId ?? string.Empty;
            return violation.Kind switch
            {
                IpcRequestContractViolationKind.RequestMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request arguments must be a JSON object.",
                    null),
                IpcRequestContractViolationKind.UnknownRequestProperty => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Request contains an unknown property: {violation.UnknownPropertyName}.",
                    null),
                IpcRequestContractViolationKind.ProtocolVersionMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'protocolVersion' is required.",
                    null),
                IpcRequestContractViolationKind.ProtocolVersionTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'protocolVersion' must be an integer.",
                    null),
                IpcRequestContractViolationKind.RequestIdMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' is required.",
                    null),
                IpcRequestContractViolationKind.RequestIdTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must be a UUID string.",
                    null),
                IpcRequestContractViolationKind.RequestIdEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must not contain leading or trailing whitespace.",
                    null),
                IpcRequestContractViolationKind.RequestIdOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must not contain leading or trailing whitespace.",
                    null),
                IpcRequestContractViolationKind.RequestIdFormatMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must be UUID format 'D'.",
                    null),
                IpcRequestContractViolationKind.OperationsMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'ops' is required.",
                    null),
                IpcRequestContractViolationKind.OperationsTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'ops' must be an array.",
                    null),
                IpcRequestContractViolationKind.OperationMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation at index {violation.OperationIndex} must be an object.",
                    null),
                IpcRequestContractViolationKind.UnknownOperationProperty => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation at index {violation.OperationIndex} contains an unknown property: {violation.UnknownPropertyName}.",
                    null),
                IpcRequestContractViolationKind.OperationIdMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation at index {violation.OperationIndex} requires property 'id'.",
                    null),
                IpcRequestContractViolationKind.OperationIdTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation at index {violation.OperationIndex} property 'id' must be a string.",
                    null),
                IpcRequestContractViolationKind.OperationIdEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation at index {violation.OperationIndex} property 'id' must not be empty or contain outer whitespace.",
                    null),
                IpcRequestContractViolationKind.OperationIdOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation at index {violation.OperationIndex} property 'id' must not be empty or contain outer whitespace.",
                    null),
                IpcRequestContractViolationKind.OperationNameMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' requires property 'op'.",
                    operationId),
                IpcRequestContractViolationKind.OperationNameTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'op' must be a string.",
                    operationId),
                IpcRequestContractViolationKind.OperationNameEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'op' must not be empty or contain outer whitespace.",
                    operationId),
                IpcRequestContractViolationKind.OperationNameOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'op' must not be empty or contain outer whitespace.",
                    operationId),
                IpcRequestContractViolationKind.OperationArgsMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' requires property 'args'.",
                    operationId),
                IpcRequestContractViolationKind.OperationArgsTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'args' must be an object.",
                    operationId),
                IpcRequestContractViolationKind.OperationAliasTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'as' must be a string when specified.",
                    operationId),
                IpcRequestContractViolationKind.OperationAliasEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'as' must not be empty or contain outer whitespace.",
                    operationId),
                IpcRequestContractViolationKind.OperationAliasOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'as' must not be empty or contain outer whitespace.",
                    operationId),
                IpcRequestContractViolationKind.ExpectationMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'expect' must be an object when specified.",
                    operationId),
                IpcRequestContractViolationKind.ExpectationContainsUnknownProperty => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'expect' contains an unknown property: {violation.UnknownPropertyName}.",
                    operationId),
                IpcRequestContractViolationKind.ExpectationMustContainAtLeastOneConstraint => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'expect' must contain at least one constraint.",
                    operationId),
                IpcRequestContractViolationKind.ExpectationBooleanConstraintMustBeBoolean => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property '{violation.PropertyPath}' must be a boolean.",
                    operationId),
                IpcRequestContractViolationKind.ExpectationIntegerConstraintMustBeInteger => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property '{violation.PropertyPath}' must be an integer.",
                    operationId),
                IpcRequestContractViolationKind.ExpectationIntegerConstraintMustBeNonNegative => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property '{violation.PropertyPath}' must be greater than or equal to 0.",
                    operationId),
                IpcRequestContractViolationKind.ExpectationCountCannotCombineWithMinOrMax => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'expect' cannot combine 'count' with 'min' or 'max'.",
                    operationId),
                IpcRequestContractViolationKind.ExpectationMinMustBeLessThanOrEqualToMax => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationId}' property 'expect' requires 'min' to be less than or equal to 'max'.",
                    operationId),
                IpcRequestContractViolationKind.DuplicatedOperationId => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation id is duplicated: {violation.DuplicatedOperationId}.",
                    violation.DuplicatedOperationId),
                _ => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request arguments are invalid.",
                    null),
            };
        }
    }
}
