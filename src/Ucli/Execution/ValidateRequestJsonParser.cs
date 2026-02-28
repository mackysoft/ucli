using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;

namespace MackySoft.Ucli.Execution;

/// <summary> Parses request JSON into <see cref="ValidateRequest" /> values for static validation. </summary>
internal sealed class ValidateRequestJsonParser : IValidateRequestJsonParser
{
    private static readonly HashSet<string> AllowedRequestProperties = new(StringComparer.Ordinal)
    {
        "protocolVersion",
        "requestId",
        "ops",
    };

    /// <summary> Parses request JSON into a validation model. </summary>
    /// <param name="requestJson"> The raw request JSON string. </param>
    /// <returns> The parse result. </returns>
    public ValidateRequestJsonParseResult Parse (string requestJson)
    {
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return ValidateRequestJsonParseResult.Failure(ExecutionError.InvalidArgument(
                "Request JSON must not be empty."));
        }

        try
        {
            using var document = JsonDocument.Parse(requestJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ValidateRequestJsonParseResult.Failure(ExecutionError.InvalidArgument(
                    "Request JSON root must be an object."));
            }

            var unknownRequestProperty = JsonPropertyGuard.FindUnknownProperty(document.RootElement, AllowedRequestProperties);
            if (unknownRequestProperty is not null)
            {
                return ValidateRequestJsonParseResult.Failure(ExecutionError.InvalidArgument(
                    $"Request contains an unknown property: {unknownRequestProperty}."));
            }

            var protocolVersion = ReadProtocolVersion(document.RootElement);
            var requestId = ReadRequestId(document.RootElement);
            if (!TryReadOperations(document.RootElement, out var operations, out var operationsError))
            {
                return ValidateRequestJsonParseResult.Failure(operationsError!);
            }

            var parsedRequest = new ValidateRequest(
                ProtocolVersion: protocolVersion,
                RequestId: requestId,
                Ops: operations);
            return ValidateRequestJsonParseResult.Success(parsedRequest);
        }
        catch (JsonException exception)
        {
            return ValidateRequestJsonParseResult.Failure(ExecutionError.InvalidArgument(
                $"Request JSON is invalid. {exception.Message}"));
        }
    }

    /// <summary> Reads protocol version from request JSON. </summary>
    /// <param name="root"> The request root object. </param>
    /// <returns> The parsed protocol version, or <c>0</c> when unavailable. </returns>
    private static int ReadProtocolVersion (JsonElement root)
    {
        if (!root.TryGetProperty("protocolVersion", out var protocolVersionElement))
        {
            return 0;
        }

        return protocolVersionElement.TryGetInt32(out var protocolVersion)
            ? protocolVersion
            : 0;
    }

    /// <summary> Reads request identifier from request JSON. </summary>
    /// <param name="root"> The request root object. </param>
    /// <returns> The request identifier, or <see langword="null" /> when unavailable. </returns>
    private static string? ReadRequestId (JsonElement root)
    {
        JsonStringContractReader.TryRead(
            jsonObject: root,
            propertyName: "requestId",
            presenceRequirement: JsonStringPresenceRequirement.OptionalLoose,
            rejectEmptyOrWhitespace: false,
            rejectOuterWhitespace: false,
            value: out var requestId,
            error: out _);
        return requestId;
    }

    /// <summary> Reads operation array from request JSON. </summary>
    /// <param name="root"> The request root object. </param>
    /// <param name="operations"> Parsed operation collection, or <see langword="null" /> when unavailable. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when operation array is valid; otherwise <see langword="false" />. </returns>
    private static bool TryReadOperations (
        JsonElement root,
        out IReadOnlyList<ValidateRequestOperation?>? operations,
        out ExecutionError? error)
    {
        error = null;

        if (!root.TryGetProperty("ops", out var operationsElement))
        {
            operations = null;
            return true;
        }

        if (operationsElement.ValueKind != JsonValueKind.Array)
        {
            operations = null;
            error = ExecutionError.InvalidArgument("Request property 'ops' must be an array.");
            return false;
        }

        var policy = RequestSchemaPolicy.PermissivePreflight;
        var parsedOperations = new List<ValidateRequestOperation?>();
        var operationIndex = 0;
        foreach (var operationElement in operationsElement.EnumerateArray())
        {
            if (operationElement.ValueKind != JsonValueKind.Object)
            {
                if (policy.RequireOperationObject)
                {
                    error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} must be an object.");
                    operations = null;
                    return false;
                }

                parsedOperations.Add(null);
                operationIndex++;
                continue;
            }

            var unknownOperationProperty = OperationContractReader.FindUnknownOperationProperty(operationElement);
            if (unknownOperationProperty is not null)
            {
                error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} contains an unknown property: {unknownOperationProperty}.");
                operations = null;
                return false;
            }

            if (!TryReadOperationStringProperty(operationElement, operationIndex, "id", policy, out var operationId, out error))
            {
                operations = null;
                return false;
            }

            if (!TryReadOperationStringProperty(operationElement, operationIndex, "op", policy, out var operationName, out error))
            {
                operations = null;
                return false;
            }

            if (!TryReadArgs(operationElement, operationIndex, out var args, out error))
            {
                operations = null;
                return false;
            }

            if (!TryValidateOperationAlias(operationElement, operationIndex, out error))
            {
                operations = null;
                return false;
            }

            if (!TryValidateExpectation(operationElement, operationIndex, out error))
            {
                operations = null;
                return false;
            }

            parsedOperations.Add(new ValidateRequestOperation(
                OpId: operationId,
                Op: operationName,
                Args: args));
            operationIndex++;
        }

        operations = parsedOperations;
        return true;
    }

    /// <summary> Reads one operation arguments object from operation JSON. </summary>
    /// <param name="operationElement"> The operation object element. </param>
    /// <param name="operationIndex"> The operation index in <c>ops</c>. </param>
    /// <param name="args"> The parsed args object. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when args is valid; otherwise <see langword="false" />. </returns>
    private static bool TryReadArgs (
        JsonElement operationElement,
        int operationIndex,
        out JsonElement args,
        out ExecutionError? error)
    {
        args = default;
        error = null;

        if (!OperationContractReader.TryReadOperationArgs(operationElement, out var parsedArgs, out var readError))
        {
            switch (readError)
            {
                case OperationObjectReadErrorKind.Missing:
                    error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'args' is required.");
                    return false;

                case OperationObjectReadErrorKind.TypeMismatch:
                    error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'args' must be an object.");
                    return false;

                default:
                    error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'args' is invalid.");
                    return false;
            }
        }

        args = parsedArgs.Clone();
        return true;
    }

    /// <summary> Reads one optional operation string property. </summary>
    /// <param name="operationElement"> The operation object element. </param>
    /// <param name="operationIndex"> The operation index in <c>ops</c>. </param>
    /// <param name="propertyName"> The property name. </param>
    /// <param name="policy"> The operation schema policy. </param>
    /// <param name="value"> The parsed property value, or <see langword="null" /> when missing or non-string. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when property contract is valid; otherwise <see langword="false" />. </returns>
    private static bool TryReadOperationStringProperty (
        JsonElement operationElement,
        int operationIndex,
        string propertyName,
        RequestSchemaPolicy policy,
        out string? value,
        out ExecutionError? error)
    {
        value = null;
        error = null;

        bool isSuccess;
        if (string.Equals(propertyName, "id", StringComparison.Ordinal))
        {
            isSuccess = OperationContractReader.TryReadOperationId(operationElement, policy, out value, out var readError);
            if (!isSuccess)
            {
                error = CreateOperationStringPropertyError(operationIndex, propertyName, readError);
            }

            return isSuccess;
        }

        if (string.Equals(propertyName, "op", StringComparison.Ordinal))
        {
            isSuccess = OperationContractReader.TryReadOperationName(operationElement, policy, out value, out var readError);
            if (!isSuccess)
            {
                error = CreateOperationStringPropertyError(operationIndex, propertyName, readError);
            }

            return isSuccess;
        }

        throw new ArgumentOutOfRangeException(nameof(propertyName), propertyName, "Unsupported operation property.");
    }

    /// <summary> Validates one optional operation alias property. </summary>
    /// <param name="operationElement"> The operation object element. </param>
    /// <param name="operationIndex"> The operation index in <c>ops</c>. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when alias contract is valid; otherwise <see langword="false" />. </returns>
    private static bool TryValidateOperationAlias (
        JsonElement operationElement,
        int operationIndex,
        out ExecutionError? error)
    {
        error = null;

        if (OperationContractReader.TryReadOperationAlias(operationElement, out _, out var readError))
        {
            return true;
        }

        switch (readError.Kind)
        {
            case JsonStringReadErrorKind.TypeMismatch:
                error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'as' must be a string when specified.");
                return false;

            case JsonStringReadErrorKind.EmptyOrWhitespace:
            case JsonStringReadErrorKind.OuterWhitespace:
                error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'as' must not be empty or contain outer whitespace.");
                return false;

            default:
                error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'as' is invalid.");
                return false;
        }
    }

    /// <summary> Validates one optional expectation object. </summary>
    /// <param name="operationElement"> The operation object element. </param>
    /// <param name="operationIndex"> The operation index in <c>ops</c>. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when expectation contract is valid; otherwise <see langword="false" />. </returns>
    private static bool TryValidateExpectation (
        JsonElement operationElement,
        int operationIndex,
        out ExecutionError? error)
    {
        error = null;
        if (ExpectationConstraintHelper.TryReadOptional(operationElement, out _, out var expectationError))
        {
            return true;
        }

        switch (expectationError.Kind)
        {
            case ExpectationConstraintReadErrorKind.ExpectationMustBeObject:
                error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'expect' must be an object when specified.");
                return false;

            case ExpectationConstraintReadErrorKind.ExpectationContainsUnknownProperty:
                error = ExecutionError.InvalidArgument(
                    $"Operation at index {operationIndex} property 'expect' contains an unknown property: {expectationError.UnknownPropertyName}.");
                return false;

            case ExpectationConstraintReadErrorKind.ExpectationMustContainAtLeastOneConstraint:
                error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'expect' must contain at least one constraint.");
                return false;

            case ExpectationConstraintReadErrorKind.BooleanConstraintMustBeBoolean:
                error = ExecutionError.InvalidArgument(
                    $"Operation at index {operationIndex} property '{expectationError.PropertyPath}' must be a boolean.");
                return false;

            case ExpectationConstraintReadErrorKind.IntegerConstraintMustBeInteger:
                error = ExecutionError.InvalidArgument(
                    $"Operation at index {operationIndex} property '{expectationError.PropertyPath}' must be an integer.");
                return false;

            case ExpectationConstraintReadErrorKind.IntegerConstraintMustBeNonNegative:
                error = ExecutionError.InvalidArgument(
                    $"Operation at index {operationIndex} property '{expectationError.PropertyPath}' must be greater than or equal to 0.");
                return false;

            case ExpectationConstraintReadErrorKind.CountCannotCombineWithMinOrMax:
                error = ExecutionError.InvalidArgument(
                    $"Operation at index {operationIndex} property 'expect' cannot combine 'count' with 'min' or 'max'.");
                return false;

            case ExpectationConstraintReadErrorKind.MinMustBeLessThanOrEqualToMax:
                error = ExecutionError.InvalidArgument(
                    $"Operation at index {operationIndex} property 'expect' requires 'min' to be less than or equal to 'max'.");
                return false;

            default:
                error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'expect' is invalid.");
                return false;
        }
    }

    /// <summary> Creates one parse error for operation string-property contract violations. </summary>
    /// <param name="operationIndex"> The operation index in <c>ops</c>. </param>
    /// <param name="propertyName"> The property name. </param>
    /// <param name="readError"> The read error details. </param>
    /// <returns> The parse error. </returns>
    private static ExecutionError CreateOperationStringPropertyError (
        int operationIndex,
        string propertyName,
        JsonStringReadError readError)
    {
        switch (readError.Kind)
        {
            case JsonStringReadErrorKind.OuterWhitespace:
                return ExecutionError.InvalidArgument(
                    $"Operation at index {operationIndex} property '{propertyName}' must not contain leading or trailing whitespace.");

            case JsonStringReadErrorKind.TypeMismatch:
                return ExecutionError.InvalidArgument(
                    $"Operation at index {operationIndex} property '{propertyName}' must be a string when specified.");

            case JsonStringReadErrorKind.EmptyOrWhitespace:
                return ExecutionError.InvalidArgument(
                    $"Operation at index {operationIndex} property '{propertyName}' must not be empty.");

            case JsonStringReadErrorKind.Missing:
                return ExecutionError.InvalidArgument(
                    $"Operation at index {operationIndex} property '{propertyName}' is required.");

            default:
                return ExecutionError.InvalidArgument(
                    $"Operation at index {operationIndex} property '{propertyName}' is invalid.");
        }
    }
}