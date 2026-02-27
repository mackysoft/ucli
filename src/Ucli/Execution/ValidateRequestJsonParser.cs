using System.Text.Json;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;

namespace MackySoft.Ucli.Execution;

/// <summary> Parses request JSON into <see cref="ValidateRequest" /> values for static validation. </summary>
internal sealed class ValidateRequestJsonParser : IValidateRequestJsonParser
{
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
            var operations = ReadOperations(document.RootElement);
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
    /// <returns> Parsed operation collection, or <see langword="null" /> when unavailable. </returns>
    private static IReadOnlyList<ValidateRequestOperation?>? ReadOperations (JsonElement root)
    {
        if (!root.TryGetProperty("ops", out var operationsElement))
        {
            return null;
        }

        if (operationsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var operations = new List<ValidateRequestOperation?>();
        foreach (var operationElement in operationsElement.EnumerateArray())
        {
            if (operationElement.ValueKind != JsonValueKind.Object)
            {
                operations.Add(null);
                continue;
            }

            var operationId = ReadStringProperty(operationElement, "id");
            var operationName = ReadStringProperty(operationElement, "op");
            var args = operationElement.TryGetProperty("args", out var argsElement)
                ? argsElement.Clone()
                : default;
            operations.Add(new ValidateRequestOperation(
                OpId: operationId,
                Op: operationName,
                Args: args));
        }

        return operations;
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
}