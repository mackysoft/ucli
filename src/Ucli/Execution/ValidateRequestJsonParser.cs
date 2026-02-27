using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;

namespace MackySoft.Ucli.Execution;

/// <summary> Parses request JSON into <see cref="ValidateRequest" /> values for static validation. </summary>
internal sealed class ValidateRequestJsonParser : IValidateRequestJsonParser
{
    private static readonly HashSet<string> AllowedOperationProperties = new(StringComparer.Ordinal)
    {
        "id",
        "op",
        "args",
        "as",
        "expect",
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
            return true;
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

            var operationId = ReadStringProperty(operationElement, "id");
            var operationName = ReadStringProperty(operationElement, "op");
            if (!TryReadArgs(operationElement, operationIndex, out var args, out error))
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
