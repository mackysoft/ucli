using System;
using System.Collections.Generic;
using System.Text.Json;
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

            var unknownRequestProperty = FindUnknownProperty(document.RootElement, AllowedRequestProperties);
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
        if (!root.TryGetProperty("requestId", out var requestIdElement))
        {
            return null;
        }

        return requestIdElement.ValueKind == JsonValueKind.String
            ? requestIdElement.GetString()
            : null;
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

        var parsedOperations = new List<ValidateRequestOperation?>();
        var operationIndex = 0;
        foreach (var operationElement in operationsElement.EnumerateArray())
        {
            if (operationElement.ValueKind != JsonValueKind.Object)
            {
                parsedOperations.Add(null);
                operationIndex++;
                continue;
            }

            var unknownOperationProperty = FindUnknownProperty(operationElement, AllowedOperationProperties);
            if (unknownOperationProperty is not null)
            {
                error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} contains an unknown property: {unknownOperationProperty}.");
                operations = null;
                return false;
            }

            if (!TryReadOperationStringProperty(operationElement, operationIndex, "id", out var operationId, out error))
            {
                operations = null;
                return false;
            }

            if (!TryReadOperationStringProperty(operationElement, operationIndex, "op", out var operationName, out error))
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

        if (!operationElement.TryGetProperty("args", out var argsElement))
        {
            error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'args' is required.");
            return false;
        }

        if (argsElement.ValueKind != JsonValueKind.Object)
        {
            error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'args' must be an object.");
            return false;
        }

        args = argsElement.Clone();
        return true;
    }

    /// <summary> Reads one optional operation string property. </summary>
    /// <param name="operationElement"> The operation object element. </param>
    /// <param name="operationIndex"> The operation index in <c>ops</c>. </param>
    /// <param name="propertyName"> The property name. </param>
    /// <param name="value"> The parsed property value, or <see langword="null" /> when missing or non-string. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when property contract is valid; otherwise <see langword="false" />. </returns>
    private static bool TryReadOperationStringProperty (
        JsonElement operationElement,
        int operationIndex,
        string propertyName,
        out string? value,
        out ExecutionError? error)
    {
        value = null;
        error = null;

        if (!operationElement.TryGetProperty(propertyName, out var propertyElement))
        {
            return true;
        }

        if (propertyElement.ValueKind != JsonValueKind.String)
        {
            return true;
        }

        var parsedValue = propertyElement.GetString()!;
        if (HasOuterWhitespace(parsedValue))
        {
            error = ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property '{propertyName}' must not contain leading or trailing whitespace.");
            return false;
        }

        value = parsedValue;
        return true;
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

        if (!operationElement.TryGetProperty("as", out var aliasElement))
        {
            return true;
        }

        if (aliasElement.ValueKind != JsonValueKind.String)
        {
            error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'as' must be a string when specified.");
            return false;
        }

        var alias = aliasElement.GetString()!;
        if (string.IsNullOrWhiteSpace(alias) || HasOuterWhitespace(alias))
        {
            error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'as' must not be empty or contain outer whitespace.");
            return false;
        }

        return true;
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
        if (!operationElement.TryGetProperty("expect", out var expectationElement))
        {
            return true;
        }

        if (expectationElement.ValueKind != JsonValueKind.Object)
        {
            error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'expect' must be an object when specified.");
            return false;
        }

        var unknownExpectationProperty = FindUnknownProperty(expectationElement, AllowedExpectationProperties);
        if (unknownExpectationProperty is not null)
        {
            error = ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'expect' contains an unknown property: {unknownExpectationProperty}.");
            return false;
        }

        if (!expectationElement.EnumerateObject().MoveNext())
        {
            error = ExecutionError.InvalidArgument($"Operation at index {operationIndex} property 'expect' must contain at least one constraint.");
            return false;
        }

        var nonNull = TryReadOptionalBooleanConstraint(expectationElement, "nonNull", operationIndex, out error);
        if (error is not null)
        {
            return false;
        }

        var count = TryReadOptionalNonNegativeIntegerConstraint(expectationElement, "count", operationIndex, out error);
        if (error is not null)
        {
            return false;
        }

        var min = TryReadOptionalNonNegativeIntegerConstraint(expectationElement, "min", operationIndex, out error);
        if (error is not null)
        {
            return false;
        }

        var max = TryReadOptionalNonNegativeIntegerConstraint(expectationElement, "max", operationIndex, out error);
        if (error is not null)
        {
            return false;
        }

        if (count.HasValue && (min.HasValue || max.HasValue))
        {
            error = ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'expect' cannot combine 'count' with 'min' or 'max'.");
            return false;
        }

        if (min.HasValue && max.HasValue && min.Value > max.Value)
        {
            error = ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'expect' requires 'min' to be less than or equal to 'max'.");
            return false;
        }

        return true;
    }

    /// <summary> Reads one optional boolean expectation constraint. </summary>
    /// <param name="expectationElement"> The expectation object. </param>
    /// <param name="propertyName"> The property name. </param>
    /// <param name="operationIndex"> The operation index in <c>ops</c>. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> The parsed value, or <see langword="null" /> when property is absent. </returns>
    private static bool? TryReadOptionalBooleanConstraint (
        JsonElement expectationElement,
        string propertyName,
        int operationIndex,
        out ExecutionError? error)
    {
        error = null;
        if (!expectationElement.TryGetProperty(propertyName, out var propertyElement))
        {
            return null;
        }

        if (propertyElement.ValueKind != JsonValueKind.True && propertyElement.ValueKind != JsonValueKind.False)
        {
            error = ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'expect.{propertyName}' must be a boolean.");
            return null;
        }

        return propertyElement.GetBoolean();
    }

    /// <summary> Reads one optional non-negative integer expectation constraint. </summary>
    /// <param name="expectationElement"> The expectation object. </param>
    /// <param name="propertyName"> The property name. </param>
    /// <param name="operationIndex"> The operation index in <c>ops</c>. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> The parsed value, or <see langword="null" /> when property is absent. </returns>
    private static int? TryReadOptionalNonNegativeIntegerConstraint (
        JsonElement expectationElement,
        string propertyName,
        int operationIndex,
        out ExecutionError? error)
    {
        error = null;
        if (!expectationElement.TryGetProperty(propertyName, out var propertyElement))
        {
            return null;
        }

        if (propertyElement.ValueKind != JsonValueKind.Number || !propertyElement.TryGetInt32(out var parsedValue))
        {
            error = ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'expect.{propertyName}' must be an integer.");
            return null;
        }

        if (parsedValue < 0)
        {
            error = ExecutionError.InvalidArgument(
                $"Operation at index {operationIndex} property 'expect.{propertyName}' must be greater than or equal to 0.");
            return null;
        }

        return parsedValue;
    }

    /// <summary> Reads one optional string property from an object element. </summary>
    /// <param name="element"> The source object element. </param>
    /// <param name="propertyName"> The property name. </param>
    /// <returns> The property string value, or <see langword="null" /> when unavailable. </returns>
    private static string? ReadStringProperty (
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyElement))
        {
            return null;
        }

        return propertyElement.ValueKind == JsonValueKind.String
            ? propertyElement.GetString()
            : null;
    }

    /// <summary> Determines whether value contains leading or trailing whitespace. </summary>
    /// <param name="value"> The source value. </param>
    /// <returns> <see langword="true" /> when leading or trailing whitespace exists; otherwise <see langword="false" />. </returns>
    private static bool HasOuterWhitespace (string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        return char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]);
    }

    /// <summary> Finds one unknown property name in a JSON object. </summary>
    /// <param name="jsonObject"> The source JSON object. </param>
    /// <param name="allowedPropertyNames"> The allowed property-name set. </param>
    /// <returns> The first unknown property name; otherwise <see langword="null" />. </returns>
    private static string? FindUnknownProperty (
        JsonElement jsonObject,
        IReadOnlySet<string> allowedPropertyNames)
    {
        foreach (var property in jsonObject.EnumerateObject())
        {
            if (!allowedPropertyNames.Contains(property.Name))
            {
                return property.Name;
            }
        }

        return null;
    }
}
