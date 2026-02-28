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
        private static readonly HashSet<string> AllowedRequestProperties = new(StringComparer.Ordinal)
        {
            "protocolVersion",
            "requestId",
            "ops",
        };

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

            var unknownRequestProperty = JsonPropertyGuard.FindUnknownProperty(request.Arguments, AllowedRequestProperties);
            if (unknownRequestProperty is not null)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: $"Request contains an unknown property: {unknownRequestProperty}.",
                    opId: null));
            }

            if (!TryReadProtocolVersion(request.Arguments, out var protocolVersion, out var protocolVersionError))
            {
                return ExecuteRequestNormalizationResult.Failure(protocolVersionError!);
            }

            if (!TryReadRequestId(request.Arguments, out var requestId, out var requestIdError))
            {
                return ExecuteRequestNormalizationResult.Failure(requestIdError!);
            }

            if (!TryReadOperations(request.Arguments, cancellationToken, out var operations, out var operationsError))
            {
                return ExecuteRequestNormalizationResult.Failure(operationsError!);
            }

            var canonicalPayload = CanonicalRequestWriter.WriteDigestPayload(protocolVersion, operations);
            var normalizedRequest = new NormalizedExecuteRequest(
                ProtocolVersion: protocolVersion,
                RequestId: requestId,
                Ops: operations,
                CanonicalDigestPayloadUtf8: canonicalPayload);
            return ExecuteRequestNormalizationResult.Success(normalizedRequest);
        }

        /// <summary> Reads and validates protocol version. </summary>
        /// <param name="requestArguments"> The request arguments object. </param>
        /// <param name="protocolVersion"> The parsed protocol version on success. </param>
        /// <param name="error"> The parse error on failure. </param>
        /// <returns> <see langword="true" /> when protocol version is valid; otherwise <see langword="false" />. </returns>
        private static bool TryReadProtocolVersion (
            JsonElement requestArguments,
            out int protocolVersion,
            out ExecuteRequestNormalizationError? error)
        {
            protocolVersion = default;
            error = null;

            if (!requestArguments.TryGetProperty("protocolVersion", out var protocolVersionElement))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument("Request property 'protocolVersion' is required.", null);
                return false;
            }

            if (!protocolVersionElement.TryGetInt32(out protocolVersion))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument("Request property 'protocolVersion' must be an integer.", null);
                return false;
            }

            if (protocolVersion != IpcProtocol.CurrentVersion)
            {
                error = ExecuteRequestNormalizationError.ProtocolVersionMismatch(
                    expectedVersion: IpcProtocol.CurrentVersion,
                    actualVersion: protocolVersion);
                return false;
            }

            return true;
        }

        /// <summary> Reads and validates request identifier. </summary>
        /// <param name="requestArguments"> The request arguments object. </param>
        /// <param name="requestId"> The normalized request identifier on success. </param>
        /// <param name="error"> The parse error on failure. </param>
        /// <returns> <see langword="true" /> when request identifier is valid; otherwise <see langword="false" />. </returns>
        private static bool TryReadRequestId (
            JsonElement requestArguments,
            out string requestId,
            out ExecuteRequestNormalizationError? error)
        {
            requestId = string.Empty;
            error = null;

            if (!JsonStringContractReader.TryRead(
                jsonObject: requestArguments,
                propertyName: "requestId",
                presenceRequirement: JsonStringPresenceRequirement.Required,
                rejectEmptyOrWhitespace: true,
                rejectOuterWhitespace: true,
                value: out var rawRequestId,
                error: out var readError))
            {
                switch (readError.Kind)
                {
                    case JsonStringReadErrorKind.Missing:
                        error = ExecuteRequestNormalizationError.InvalidArgument("Request property 'requestId' is required.", null);
                        return false;

                    case JsonStringReadErrorKind.TypeMismatch:
                        error = ExecuteRequestNormalizationError.InvalidArgument("Request property 'requestId' must be a UUID string.", null);
                        return false;

                    case JsonStringReadErrorKind.EmptyOrWhitespace:
                    case JsonStringReadErrorKind.OuterWhitespace:
                        error = ExecuteRequestNormalizationError.InvalidArgument("Request property 'requestId' must not contain leading or trailing whitespace.", null);
                        return false;

                    default:
                        error = ExecuteRequestNormalizationError.InvalidArgument("Request property 'requestId' is invalid.", null);
                        return false;
                }
            }

            if (!Guid.TryParseExact(rawRequestId!, "D", out var parsedRequestId))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument("Request property 'requestId' must be UUID format 'D'.", null);
                return false;
            }

            requestId = parsedRequestId.ToString("D");
            return true;
        }

        /// <summary> Reads and validates operation list. </summary>
        /// <param name="requestArguments"> The request arguments object. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <param name="operations"> The normalized operation list on success. </param>
        /// <param name="error"> The parse error on failure. </param>
        /// <returns> <see langword="true" /> when operation list is valid; otherwise <see langword="false" />. </returns>
        private static bool TryReadOperations (
            JsonElement requestArguments,
            CancellationToken cancellationToken,
            out List<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError? error)
        {
            operations = new List<NormalizedOperation>();
            error = null;

            if (!requestArguments.TryGetProperty("ops", out var operationsElement))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument("Request property 'ops' is required.", null);
                return false;
            }

            if (operationsElement.ValueKind != JsonValueKind.Array)
            {
                error = ExecuteRequestNormalizationError.InvalidArgument("Request property 'ops' must be an array.", null);
                return false;
            }

            var policy = RequestSchemaPolicy.StrictExecute;
            var duplicatedOpIdDetector = new HashSet<string>(StringComparer.Ordinal);
            var operationIndex = 0;
            foreach (var operationElement in operationsElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (operationElement.ValueKind != JsonValueKind.Object)
                {
                    if (policy.RequireOperationObject)
                    {
                        error = ExecuteRequestNormalizationError.InvalidArgument($"Operation at index {operationIndex} must be an object.", null);
                        return false;
                    }

                    operationIndex++;
                    continue;
                }

                var unknownOperationProperty = OperationContractReader.FindUnknownOperationProperty(operationElement);
                if (unknownOperationProperty is not null)
                {
                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Operation at index {operationIndex} contains an unknown property: {unknownOperationProperty}.",
                        null);
                    return false;
                }

                if (!TryReadOperationId(operationElement, operationIndex, policy, out var operationId, out error))
                {
                    return false;
                }

                if (!duplicatedOpIdDetector.Add(operationId))
                {
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation id is duplicated: {operationId}.", operationId);
                    return false;
                }

                if (!TryReadOperationName(operationElement, policy, operationId, out var operationName, out error))
                {
                    return false;
                }

                if (!TryReadOperationArgs(operationElement, operationId, out var operationArgs, out error))
                {
                    return false;
                }

                if (!TryReadOperationAs(operationElement, operationId, out var operationAs, out error))
                {
                    return false;
                }

                if (!TryReadExpectation(operationElement, operationId, out var expectation, out error))
                {
                    return false;
                }

                operations.Add(new NormalizedOperation(
                    Id: operationId,
                    Op: operationName,
                    Args: operationArgs.Clone(),
                    As: operationAs,
                    Expect: expectation));
                operationIndex++;
            }

            return true;
        }

        /// <summary> Reads and validates operation identifier. </summary>
        /// <param name="operationElement"> The operation object element. </param>
        /// <param name="operationIndex"> The operation index in <c>ops</c>. </param>
        /// <param name="policy"> The operation schema policy. </param>
        /// <param name="operationId"> The operation identifier on success. </param>
        /// <param name="error"> The parse error on failure. </param>
        /// <returns> <see langword="true" /> when operation identifier is valid; otherwise <see langword="false" />. </returns>
        private static bool TryReadOperationId (
            JsonElement operationElement,
            int operationIndex,
            RequestSchemaPolicy policy,
            out string operationId,
            out ExecuteRequestNormalizationError? error)
        {
            operationId = string.Empty;
            error = null;

            if (OperationContractReader.TryReadOperationId(operationElement, policy, out var parsedOperationId, out var readError))
            {
                operationId = parsedOperationId!;
                return true;
            }

            switch (readError.Kind)
            {
                case JsonStringReadErrorKind.Missing:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation at index {operationIndex} requires property 'id'.", null);
                    return false;

                case JsonStringReadErrorKind.TypeMismatch:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation at index {operationIndex} property 'id' must be a string.", null);
                    return false;

                case JsonStringReadErrorKind.EmptyOrWhitespace:
                case JsonStringReadErrorKind.OuterWhitespace:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation at index {operationIndex} property 'id' must not be empty or contain outer whitespace.", null);
                    return false;

                default:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation at index {operationIndex} property 'id' is invalid.", null);
                    return false;
            }
        }

        /// <summary> Reads and validates operation name. </summary>
        /// <param name="operationElement"> The operation object element. </param>
        /// <param name="policy"> The operation schema policy. </param>
        /// <param name="operationId"> The operation identifier. </param>
        /// <param name="operationName"> The operation name on success. </param>
        /// <param name="error"> The parse error on failure. </param>
        /// <returns> <see langword="true" /> when operation name is valid; otherwise <see langword="false" />. </returns>
        private static bool TryReadOperationName (
            JsonElement operationElement,
            RequestSchemaPolicy policy,
            string operationId,
            out string operationName,
            out ExecuteRequestNormalizationError? error)
        {
            operationName = string.Empty;
            error = null;

            if (OperationContractReader.TryReadOperationName(operationElement, policy, out var parsedOperationName, out var readError))
            {
                operationName = parsedOperationName!;
                return true;
            }

            switch (readError.Kind)
            {
                case JsonStringReadErrorKind.Missing:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' requires property 'op'.", operationId);
                    return false;

                case JsonStringReadErrorKind.TypeMismatch:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' property 'op' must be a string.", operationId);
                    return false;

                case JsonStringReadErrorKind.EmptyOrWhitespace:
                case JsonStringReadErrorKind.OuterWhitespace:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' property 'op' must not be empty or contain outer whitespace.", operationId);
                    return false;

                default:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' property 'op' is invalid.", operationId);
                    return false;
            }
        }

        /// <summary> Reads and validates operation arguments. </summary>
        /// <param name="operationElement"> The operation object element. </param>
        /// <param name="operationId"> The operation identifier. </param>
        /// <param name="operationArgs"> The operation arguments on success. </param>
        /// <param name="error"> The parse error on failure. </param>
        /// <returns> <see langword="true" /> when operation arguments are valid; otherwise <see langword="false" />. </returns>
        private static bool TryReadOperationArgs (
            JsonElement operationElement,
            string operationId,
            out JsonElement operationArgs,
            out ExecuteRequestNormalizationError? error)
        {
            operationArgs = default;
            error = null;

            if (OperationContractReader.TryReadOperationArgs(operationElement, out var parsedArgs, out var argsReadError))
            {
                operationArgs = parsedArgs;
                return true;
            }

            switch (argsReadError)
            {
                case OperationObjectReadErrorKind.Missing:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' requires property 'args'.", operationId);
                    return false;

                case OperationObjectReadErrorKind.TypeMismatch:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' property 'args' must be an object.", operationId);
                    return false;

                default:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' property 'args' is invalid.", operationId);
                    return false;
            }
        }

        /// <summary> Reads and validates operation alias. </summary>
        /// <param name="operationElement"> The operation object element. </param>
        /// <param name="operationId"> The operation identifier. </param>
        /// <param name="operationAs"> The alias value on success. </param>
        /// <param name="error"> The parse error on failure. </param>
        /// <returns> <see langword="true" /> when alias value is valid; otherwise <see langword="false" />. </returns>
        private static bool TryReadOperationAs (
            JsonElement operationElement,
            string operationId,
            out string? operationAs,
            out ExecuteRequestNormalizationError? error)
        {
            operationAs = null;
            error = null;

            if (OperationContractReader.TryReadOperationAlias(operationElement, out var parsedOperationAs, out var readError))
            {
                operationAs = parsedOperationAs;
                return true;
            }

            switch (readError.Kind)
            {
                case JsonStringReadErrorKind.TypeMismatch:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' property 'as' must be a string when specified.", operationId);
                    return false;

                case JsonStringReadErrorKind.EmptyOrWhitespace:
                case JsonStringReadErrorKind.OuterWhitespace:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' property 'as' must not be empty or contain outer whitespace.", operationId);
                    return false;

                default:
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' property 'as' is invalid.", operationId);
                    return false;
            }
        }

        /// <summary> Reads and validates shared expectation constraints. </summary>
        /// <param name="operationElement"> The operation object element. </param>
        /// <param name="operationId"> The operation identifier. </param>
        /// <param name="expectation"> The normalized expectation on success. </param>
        /// <param name="error"> The parse error on failure. </param>
        /// <returns> <see langword="true" /> when expectation contract is valid; otherwise <see langword="false" />. </returns>
        private static bool TryReadExpectation (
            JsonElement operationElement,
            string operationId,
            out NormalizedExpectation? expectation,
            out ExecuteRequestNormalizationError? error)
        {
            expectation = null;
            error = null;

            if (!ExpectationConstraintHelper.TryReadOptional(operationElement, out var expectationConstraints, out var expectationReadError))
            {
                switch (expectationReadError.Kind)
                {
                    case ExpectationConstraintReadErrorKind.ExpectationMustBeObject:
                        error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' property 'expect' must be an object when specified.", operationId);
                        return false;

                    case ExpectationConstraintReadErrorKind.ExpectationContainsUnknownProperty:
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            $"Operation '{operationId}' property 'expect' contains an unknown property: {expectationReadError.UnknownPropertyName}.",
                            operationId);
                        return false;

                    case ExpectationConstraintReadErrorKind.ExpectationMustContainAtLeastOneConstraint:
                        error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' property 'expect' must contain at least one constraint.", operationId);
                        return false;

                    case ExpectationConstraintReadErrorKind.BooleanConstraintMustBeBoolean:
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            $"Operation '{operationId}' property '{expectationReadError.PropertyPath}' must be a boolean.",
                            operationId);
                        return false;

                    case ExpectationConstraintReadErrorKind.IntegerConstraintMustBeInteger:
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            $"Operation '{operationId}' property '{expectationReadError.PropertyPath}' must be an integer.",
                            operationId);
                        return false;

                    case ExpectationConstraintReadErrorKind.IntegerConstraintMustBeNonNegative:
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            $"Operation '{operationId}' property '{expectationReadError.PropertyPath}' must be greater than or equal to 0.",
                            operationId);
                        return false;

                    case ExpectationConstraintReadErrorKind.CountCannotCombineWithMinOrMax:
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            $"Operation '{operationId}' property 'expect' cannot combine 'count' with 'min' or 'max'.",
                            operationId);
                        return false;

                    case ExpectationConstraintReadErrorKind.MinMustBeLessThanOrEqualToMax:
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            $"Operation '{operationId}' property 'expect' requires 'min' to be less than or equal to 'max'.",
                            operationId);
                        return false;

                    default:
                        error = ExecuteRequestNormalizationError.InvalidArgument($"Operation '{operationId}' property 'expect' is invalid.", operationId);
                        return false;
                }
            }

            if (!expectationConstraints.HasValue)
            {
                return true;
            }

            var parsedExpectation = expectationConstraints.Value;
            expectation = new NormalizedExpectation(
                NonNull: parsedExpectation.NonNull,
                Count: parsedExpectation.Count,
                Min: parsedExpectation.Min,
                Max: parsedExpectation.Max);
            return true;
        }

    }
}
