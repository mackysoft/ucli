using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;

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

        private static readonly HashSet<string> AllowedOperationProperties = new(StringComparer.Ordinal)
        {
            "id",
            "op",
            "args",
            "as",
            "expect",
        };

        private static readonly HashSet<string> AllowedExpectationProperties = new(StringComparer.Ordinal)
        {
            "nonNull",
            "count",
            "min",
            "max",
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
                return ExecuteRequestNormalizationResult.Failure(CreateInvalidArgument(
                    message: $"Execute command is not supported: {request.Command}.",
                    opId: null));
            }

            if (request.Arguments.ValueKind != JsonValueKind.Object)
            {
                return ExecuteRequestNormalizationResult.Failure(CreateInvalidArgument(
                    message: "Request arguments must be a JSON object.",
                    opId: null));
            }

            var unknownRequestProperty = FindUnknownProperty(request.Arguments, AllowedRequestProperties);
            if (unknownRequestProperty is not null)
            {
                return ExecuteRequestNormalizationResult.Failure(CreateInvalidArgument(
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
            error = CreateInvalidArgument("Request property 'protocolVersion' is required.", null);
            return false;
        }

        if (!protocolVersionElement.TryGetInt32(out protocolVersion))
        {
            error = CreateInvalidArgument("Request property 'protocolVersion' must be an integer.", null);
            return false;
        }

        if (protocolVersion != IpcProtocol.CurrentVersion)
        {
            error = new ExecuteRequestNormalizationError(
                Code: IpcErrorCodes.ProtocolVersionMismatch,
                Message: $"Protocol version mismatch. Expected {IpcProtocol.CurrentVersion}, actual {protocolVersion}.",
                OpId: null);
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

        if (!requestArguments.TryGetProperty("requestId", out var requestIdElement))
        {
            error = CreateInvalidArgument("Request property 'requestId' is required.", null);
            return false;
        }

        if (requestIdElement.ValueKind != JsonValueKind.String)
        {
            error = CreateInvalidArgument("Request property 'requestId' must be a UUID string.", null);
            return false;
        }

        var rawRequestId = requestIdElement.GetString()!;
        if (HasOuterWhitespace(rawRequestId) || string.IsNullOrWhiteSpace(rawRequestId))
        {
            error = CreateInvalidArgument("Request property 'requestId' must not contain leading or trailing whitespace.", null);
            return false;
        }

        if (!Guid.TryParseExact(rawRequestId, "D", out var parsedRequestId))
        {
            error = CreateInvalidArgument("Request property 'requestId' must be UUID format 'D'.", null);
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
            error = CreateInvalidArgument("Request property 'ops' is required.", null);
            return false;
        }

        if (operationsElement.ValueKind != JsonValueKind.Array)
        {
            error = CreateInvalidArgument("Request property 'ops' must be an array.", null);
            return false;
        }

        var duplicatedOpIdDetector = new HashSet<string>(StringComparer.Ordinal);
        var operationIndex = 0;
        foreach (var operationElement in operationsElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (operationElement.ValueKind != JsonValueKind.Object)
            {
                error = CreateInvalidArgument($"Operation at index {operationIndex} must be an object.", null);
                return false;
            }

            var unknownOperationProperty = FindUnknownProperty(operationElement, AllowedOperationProperties);
            if (unknownOperationProperty is not null)
            {
                error = CreateInvalidArgument(
                    $"Operation at index {operationIndex} contains an unknown property: {unknownOperationProperty}.",
                    null);
                return false;
            }

            if (!TryReadOperationId(operationElement, operationIndex, out var operationId, out error))
            {
                return false;
            }

            if (!duplicatedOpIdDetector.Add(operationId))
            {
                error = CreateInvalidArgument($"Operation id is duplicated: {operationId}.", operationId);
                return false;
            }

            if (!TryReadOperationName(operationElement, operationId, out var operationName, out error))
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
    /// <param name="operationId"> The operation identifier on success. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when operation identifier is valid; otherwise <see langword="false" />. </returns>
    private static bool TryReadOperationId (
        JsonElement operationElement,
        int operationIndex,
        out string operationId,
        out ExecuteRequestNormalizationError? error)
    {
        operationId = string.Empty;
        error = null;

        if (!operationElement.TryGetProperty("id", out var operationIdElement))
        {
            error = CreateInvalidArgument($"Operation at index {operationIndex} requires property 'id'.", null);
            return false;
        }

        if (operationIdElement.ValueKind != JsonValueKind.String)
        {
            error = CreateInvalidArgument($"Operation at index {operationIndex} property 'id' must be a string.", null);
            return false;
        }

        operationId = operationIdElement.GetString()!;
        if (string.IsNullOrWhiteSpace(operationId) || HasOuterWhitespace(operationId))
        {
            error = CreateInvalidArgument($"Operation at index {operationIndex} property 'id' must not be empty or contain outer whitespace.", null);
            return false;
        }

        return true;
    }

    /// <summary> Reads and validates operation name. </summary>
    /// <param name="operationElement"> The operation object element. </param>
    /// <param name="operationId"> The operation identifier. </param>
    /// <param name="operationName"> The operation name on success. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when operation name is valid; otherwise <see langword="false" />. </returns>
    private static bool TryReadOperationName (
        JsonElement operationElement,
        string operationId,
        out string operationName,
        out ExecuteRequestNormalizationError? error)
    {
        operationName = string.Empty;
        error = null;

        if (!operationElement.TryGetProperty("op", out var operationNameElement))
        {
            error = CreateInvalidArgument($"Operation '{operationId}' requires property 'op'.", operationId);
            return false;
        }

        if (operationNameElement.ValueKind != JsonValueKind.String)
        {
            error = CreateInvalidArgument($"Operation '{operationId}' property 'op' must be a string.", operationId);
            return false;
        }

        operationName = operationNameElement.GetString()!;
        if (string.IsNullOrWhiteSpace(operationName) || HasOuterWhitespace(operationName))
        {
            error = CreateInvalidArgument($"Operation '{operationId}' property 'op' must not be empty or contain outer whitespace.", operationId);
            return false;
        }

        return true;
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

        if (!operationElement.TryGetProperty("args", out operationArgs))
        {
            error = CreateInvalidArgument($"Operation '{operationId}' requires property 'args'.", operationId);
            return false;
        }

        if (operationArgs.ValueKind != JsonValueKind.Object)
        {
            error = CreateInvalidArgument($"Operation '{operationId}' property 'args' must be an object.", operationId);
            return false;
        }

        return true;
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

        if (!operationElement.TryGetProperty("as", out var operationAsElement))
        {
            return true;
        }

        if (operationAsElement.ValueKind != JsonValueKind.String)
        {
            error = CreateInvalidArgument($"Operation '{operationId}' property 'as' must be a string when specified.", operationId);
            return false;
        }

        var alias = operationAsElement.GetString()!;
        if (string.IsNullOrWhiteSpace(alias) || HasOuterWhitespace(alias))
        {
            error = CreateInvalidArgument($"Operation '{operationId}' property 'as' must not be empty or contain outer whitespace.", operationId);
            return false;
        }

        operationAs = alias;
        return true;
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

        if (!operationElement.TryGetProperty("expect", out var expectationElement))
        {
            return true;
        }

        if (expectationElement.ValueKind != JsonValueKind.Object)
        {
            error = CreateInvalidArgument($"Operation '{operationId}' property 'expect' must be an object when specified.", operationId);
            return false;
        }

        var unknownExpectationProperty = FindUnknownProperty(expectationElement, AllowedExpectationProperties);
        if (unknownExpectationProperty is not null)
        {
            error = CreateInvalidArgument(
                $"Operation '{operationId}' property 'expect' contains an unknown property: {unknownExpectationProperty}.",
                operationId);
            return false;
        }

        if (!expectationElement.EnumerateObject().MoveNext())
        {
            error = CreateInvalidArgument($"Operation '{operationId}' property 'expect' must contain at least one constraint.", operationId);
            return false;
        }

        var nonNull = TryReadOptionalBooleanConstraint(expectationElement, "nonNull", operationId, out error);
        if (error is not null)
        {
            return false;
        }

        var count = TryReadOptionalNonNegativeIntegerConstraint(expectationElement, "count", operationId, out error);
        if (error is not null)
        {
            return false;
        }

        var min = TryReadOptionalNonNegativeIntegerConstraint(expectationElement, "min", operationId, out error);
        if (error is not null)
        {
            return false;
        }

        var max = TryReadOptionalNonNegativeIntegerConstraint(expectationElement, "max", operationId, out error);
        if (error is not null)
        {
            return false;
        }

        if (count.HasValue && (min.HasValue || max.HasValue))
        {
            error = CreateInvalidArgument(
                $"Operation '{operationId}' property 'expect' cannot combine 'count' with 'min' or 'max'.",
                operationId);
            return false;
        }

        if (min.HasValue && max.HasValue && min.Value > max.Value)
        {
            error = CreateInvalidArgument(
                $"Operation '{operationId}' property 'expect' requires 'min' to be less than or equal to 'max'.",
                operationId);
            return false;
        }

        expectation = new NormalizedExpectation(
            NonNull: nonNull,
            Count: count,
            Min: min,
            Max: max);
        return true;
    }

    /// <summary> Reads one optional boolean expectation constraint. </summary>
    /// <param name="expectationElement"> The expectation object. </param>
    /// <param name="propertyName"> The property to parse. </param>
    /// <param name="operationId"> The operation identifier. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> The parsed value, or <see langword="null" /> when property is absent. </returns>
    private static bool? TryReadOptionalBooleanConstraint (
        JsonElement expectationElement,
        string propertyName,
        string operationId,
        out ExecuteRequestNormalizationError? error)
    {
        error = null;
        if (!expectationElement.TryGetProperty(propertyName, out var propertyElement))
        {
            return null;
        }

        if (propertyElement.ValueKind != JsonValueKind.True && propertyElement.ValueKind != JsonValueKind.False)
        {
            error = CreateInvalidArgument(
                $"Operation '{operationId}' property 'expect.{propertyName}' must be a boolean.",
                operationId);
            return null;
        }

        return propertyElement.GetBoolean();
    }

    /// <summary> Reads one optional non-negative integer expectation constraint. </summary>
    /// <param name="expectationElement"> The expectation object. </param>
    /// <param name="propertyName"> The property to parse. </param>
    /// <param name="operationId"> The operation identifier. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> The parsed value, or <see langword="null" /> when property is absent. </returns>
    private static int? TryReadOptionalNonNegativeIntegerConstraint (
        JsonElement expectationElement,
        string propertyName,
        string operationId,
        out ExecuteRequestNormalizationError? error)
    {
        error = null;
        if (!expectationElement.TryGetProperty(propertyName, out var propertyElement))
        {
            return null;
        }

        if (propertyElement.ValueKind != JsonValueKind.Number || !propertyElement.TryGetInt32(out var parsedValue))
        {
            error = CreateInvalidArgument(
                $"Operation '{operationId}' property 'expect.{propertyName}' must be an integer.",
                operationId);
            return null;
        }

        if (parsedValue < 0)
        {
            error = CreateInvalidArgument(
                $"Operation '{operationId}' property 'expect.{propertyName}' must be greater than or equal to 0.",
                operationId);
            return null;
        }

        return parsedValue;
    }

    /// <summary> Finds the first unknown property in a JSON object. </summary>
    /// <param name="jsonObject"> The object to inspect. </param>
    /// <param name="allowedProperties"> The allowed property set. </param>
    /// <returns> The unknown property name, or <see langword="null" /> when all properties are allowed. </returns>
    private static string? FindUnknownProperty (
        JsonElement jsonObject,
        HashSet<string> allowedProperties)
    {
        foreach (var property in jsonObject.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                return property.Name;
            }
        }

        return null;
    }

    /// <summary> Determines whether string contains leading or trailing whitespace. </summary>
    /// <param name="value"> The string to inspect. </param>
    /// <returns> <see langword="true" /> when leading or trailing whitespace exists; otherwise <see langword="false" />. </returns>
    private static bool HasOuterWhitespace (string value)
    {
        return value.Length != value.Trim().Length;
    }

    /// <summary> Creates one invalid-argument normalization error. </summary>
    /// <param name="message"> The error message. </param>
    /// <param name="opId"> The related operation identifier. </param>
    /// <returns> The normalization error. </returns>
        private static ExecuteRequestNormalizationError CreateInvalidArgument (
            string message,
            string? opId)
        {
            return new ExecuteRequestNormalizationError(
                Code: IpcErrorCodes.InvalidArgument,
                Message: message,
                OpId: opId);
        }
    }
}
