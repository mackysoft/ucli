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
            if (!ValidateRequestOperationReader.TryReadOperations(document.RootElement, out var operations, out var operationsError))
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
}