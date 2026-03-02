using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Provides reusable readers for request root and operation contracts. </summary>
internal static class IpcRequestContractReader
{
    private static readonly HashSet<string> AllowedRequestProperties = new(StringComparer.Ordinal)
    {
        "protocolVersion",
        "requestId",
        "ops",
    };

    /// <summary> Reads one request contract according to the provided profile. </summary>
    /// <param name="requestObject"> The request JSON object. </param>
    /// <param name="profile"> The read profile that controls strictness and fallback behavior. </param>
    /// <param name="requestContract"> The parsed request contract. </param>
    /// <param name="error"> The machine-readable read error on failure. </param>
    /// <returns> <see langword="true" /> when the contract is satisfied; otherwise <see langword="false" />. </returns>
    public static bool TryRead (
        JsonElement requestObject,
        in IpcRequestContractReadProfile profile,
        out IpcRequestContract requestContract,
        out IpcRequestContractReadError error)
    {
        requestContract = default!;
        if (requestObject.ValueKind != JsonValueKind.Object)
        {
            error = IpcRequestContractReadError.RequestMustBeObject();
            return false;
        }

        var unknownRequestProperty = JsonPropertyGuard.FindUnknownProperty(requestObject, AllowedRequestProperties);
        if (unknownRequestProperty is not null)
        {
            error = IpcRequestContractReadError.UnknownRequestProperty(unknownRequestProperty);
            return false;
        }

        if (!TryReadProtocolVersion(requestObject, profile, out var protocolVersion, out error))
        {
            return false;
        }

        if (!TryReadRequestId(requestObject, profile, out var requestId, out error))
        {
            return false;
        }

        if (!TryReadOperations(requestObject, profile, out var operations, out error))
        {
            return false;
        }

        requestContract = new IpcRequestContract(
            ProtocolVersion: protocolVersion,
            RequestId: requestId,
            Operations: operations);
        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadProtocolVersion (
        JsonElement requestObject,
        in IpcRequestContractReadProfile profile,
        out int protocolVersion,
        out IpcRequestContractReadError error)
    {
        protocolVersion = 0;
        if (!requestObject.TryGetProperty("protocolVersion", out var protocolVersionElement))
        {
            if (profile.RequireProtocolVersion)
            {
                error = IpcRequestContractReadError.ProtocolVersionMissing();
                return false;
            }

            error = IpcRequestContractReadError.None;
            return true;
        }

        if (!protocolVersionElement.TryGetInt32(out protocolVersion))
        {
            if (profile.RequireProtocolVersion)
            {
                error = IpcRequestContractReadError.ProtocolVersionTypeMismatch();
                return false;
            }

            protocolVersion = 0;
            error = IpcRequestContractReadError.None;
            return true;
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadRequestId (
        JsonElement requestObject,
        in IpcRequestContractReadProfile profile,
        out string? requestId,
        out IpcRequestContractReadError error)
    {
        if (!JsonStringContractReader.TryRead(
            jsonObject: requestObject,
            propertyName: "requestId",
            presenceRequirement: profile.RequireRequestId
                ? JsonStringPresenceRequirement.Required
                : JsonStringPresenceRequirement.OptionalLoose,
            rejectEmptyOrWhitespace: profile.RequireNonEmptyRequestId,
            rejectOuterWhitespace: profile.RejectRequestIdOuterWhitespace,
            value: out requestId,
            error: out var readError))
        {
            error = IpcRequestContractReadError.RequestIdContractViolation(readError);
            return false;
        }

        if (!profile.RequireCanonicalRequestIdFormat || requestId is null)
        {
            error = IpcRequestContractReadError.None;
            return true;
        }

        if (!Guid.TryParseExact(requestId, "D", out var parsedRequestId))
        {
            error = IpcRequestContractReadError.RequestIdFormatMismatch();
            return false;
        }

        requestId = parsedRequestId.ToString("D");
        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadOperations (
        JsonElement requestObject,
        in IpcRequestContractReadProfile profile,
        out IReadOnlyList<IpcRequestContractOperation?>? operations,
        out IpcRequestContractReadError error)
    {
        operations = null;
        if (!requestObject.TryGetProperty("ops", out var operationsElement))
        {
            if (profile.RequireOperations)
            {
                error = IpcRequestContractReadError.OperationsMissing();
                return false;
            }

            error = IpcRequestContractReadError.None;
            return true;
        }

        if (operationsElement.ValueKind != JsonValueKind.Array)
        {
            error = IpcRequestContractReadError.OperationsTypeMismatch();
            return false;
        }

        var parsedOperations = new List<IpcRequestContractOperation?>();
        HashSet<string>? duplicateOperationIdDetector = profile.RejectDuplicatedOperationId
            ? new HashSet<string>(StringComparer.Ordinal)
            : null;

        var operationIndex = 0;
        foreach (var operationElement in operationsElement.EnumerateArray())
        {
            if (!TryReadOperationElement(
                operationElement,
                operationIndex,
                profile,
                duplicateOperationIdDetector,
                out var operation,
                out error))
            {
                operations = null;
                return false;
            }

            parsedOperations.Add(operation);
            operationIndex++;
        }

        operations = parsedOperations;
        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadOperationElement (
        JsonElement operationElement,
        int operationIndex,
        in IpcRequestContractReadProfile profile,
        HashSet<string>? duplicateOperationIdDetector,
        out IpcRequestContractOperation? operation,
        out IpcRequestContractReadError error)
    {
        operation = null;
        if (operationElement.ValueKind != JsonValueKind.Object)
        {
            if (profile.OperationSchemaPolicy.RequireOperationObject)
            {
                error = IpcRequestContractReadError.OperationMustBeObject(operationIndex);
                return false;
            }

            error = IpcRequestContractReadError.None;
            return true;
        }

        var unknownOperationProperty = OperationContractReader.FindUnknownOperationProperty(operationElement);
        if (unknownOperationProperty is not null)
        {
            error = IpcRequestContractReadError.UnknownOperationProperty(operationIndex, unknownOperationProperty);
            return false;
        }

        if (!OperationContractReader.TryReadOperationId(
            operationElement,
            profile.OperationSchemaPolicy,
            out var operationId,
            out var operationIdReadError))
        {
            error = IpcRequestContractReadError.OperationIdContractViolation(operationIndex, operationIdReadError);
            return false;
        }

        if (!OperationContractReader.TryReadOperationName(
            operationElement,
            profile.OperationSchemaPolicy,
            out var operationName,
            out var operationNameReadError))
        {
            error = IpcRequestContractReadError.OperationNameContractViolation(operationIndex, operationId, operationNameReadError);
            return false;
        }

        if (!OperationContractReader.TryReadOperationArgs(
            operationElement,
            out var operationArgs,
            out var operationArgsReadError))
        {
            error = IpcRequestContractReadError.OperationArgsContractViolation(operationIndex, operationId, operationArgsReadError);
            return false;
        }

        if (!OperationContractReader.TryReadOperationAlias(
            operationElement,
            out var operationAlias,
            out var operationAliasReadError))
        {
            error = IpcRequestContractReadError.OperationAliasContractViolation(operationIndex, operationId, operationAliasReadError);
            return false;
        }

        if (!ExpectationConstraintHelper.TryReadOptional(
            operationElement,
            out var expectationConstraints,
            out var expectationReadError))
        {
            error = IpcRequestContractReadError.OperationExpectationContractViolation(operationIndex, operationId, expectationReadError);
            return false;
        }

        if (operationId is not null
            && duplicateOperationIdDetector is not null
            && !duplicateOperationIdDetector.Add(operationId))
        {
            error = IpcRequestContractReadError.DuplicatedOperationIdError(operationIndex, operationId);
            return false;
        }

        operation = new IpcRequestContractOperation(
            Id: operationId,
            Name: operationName,
            Args: operationArgs.Clone(),
            Alias: operationAlias,
            Expectation: expectationConstraints);
        error = IpcRequestContractReadError.None;
        return true;
    }
}