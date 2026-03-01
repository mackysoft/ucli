using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;

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
            var normalizedPlanToken = string.IsNullOrWhiteSpace(request.PlanToken)
                ? null
                : request.PlanToken.Trim();
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
            return readError.Kind switch
            {
                IpcRequestContractReadErrorKind.RequestMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request arguments must be a JSON object.",
                    null),
                IpcRequestContractReadErrorKind.UnknownRequestProperty => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Request contains an unknown property: {readError.UnknownPropertyName}.",
                    null),
                IpcRequestContractReadErrorKind.ProtocolVersionMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'protocolVersion' is required.",
                    null),
                IpcRequestContractReadErrorKind.ProtocolVersionTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'protocolVersion' must be an integer.",
                    null),
                IpcRequestContractReadErrorKind.RequestIdContractViolation => ExecuteRequestNormalizationErrorFactory.RequestId(
                    readError.JsonStringReadError),
                IpcRequestContractReadErrorKind.RequestIdFormatMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must be UUID format 'D'.",
                    null),
                IpcRequestContractReadErrorKind.OperationsMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'ops' is required.",
                    null),
                IpcRequestContractReadErrorKind.OperationsTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'ops' must be an array.",
                    null),
                IpcRequestContractReadErrorKind.OperationMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation at index {readError.OperationIndex} must be an object.",
                    null),
                IpcRequestContractReadErrorKind.UnknownOperationProperty => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation at index {readError.OperationIndex} contains an unknown property: {readError.UnknownPropertyName}.",
                    null),
                IpcRequestContractReadErrorKind.OperationIdContractViolation => ExecuteRequestNormalizationErrorFactory.OperationId(
                    readError.OperationIndex,
                    readError.JsonStringReadError),
                IpcRequestContractReadErrorKind.OperationNameContractViolation => ExecuteRequestNormalizationErrorFactory.OperationName(
                    readError.OperationId ?? string.Empty,
                    readError.JsonStringReadError),
                IpcRequestContractReadErrorKind.OperationArgsContractViolation => ExecuteRequestNormalizationErrorFactory.OperationArgs(
                    readError.OperationId ?? string.Empty,
                    readError.OperationObjectReadErrorKind),
                IpcRequestContractReadErrorKind.OperationAliasContractViolation => ExecuteRequestNormalizationErrorFactory.OperationAlias(
                    readError.OperationId ?? string.Empty,
                    readError.JsonStringReadError),
                IpcRequestContractReadErrorKind.OperationExpectationContractViolation => ExecuteRequestNormalizationErrorFactory.OperationExpectation(
                    readError.OperationId ?? string.Empty,
                    readError.ExpectationReadError),
                IpcRequestContractReadErrorKind.DuplicatedOperationId => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation id is duplicated: {readError.DuplicatedOperationId}.",
                    readError.DuplicatedOperationId),
                _ => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request arguments are invalid.",
                    null),
            };
        }
    }
}