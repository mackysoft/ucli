using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;

namespace MackySoft.Ucli.Execution;

/// <summary> Reads operation contracts from preflight request JSON payloads. </summary>
internal static class ValidateRequestOperationReader
{
    /// <summary> Reads operation list from request root. </summary>
    /// <param name="root"> The request root object. </param>
    /// <param name="operations"> Parsed operation collection, or <see langword="null" /> when unavailable. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when operation array contract is valid; otherwise <see langword="false" />. </returns>
    public static bool TryReadOperations (
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
                    operations = null;
                    error = ValidateRequestParseErrorFactory.OperationMustBeObject(operationIndex);
                    return false;
                }

                parsedOperations.Add(null);
                operationIndex++;
                continue;
            }

            var unknownOperationProperty = OperationContractReader.FindUnknownOperationProperty(operationElement);
            if (unknownOperationProperty is not null)
            {
                operations = null;
                error = ValidateRequestParseErrorFactory.UnknownOperationProperty(operationIndex, unknownOperationProperty);
                return false;
            }

            if (!TryReadOperationId(operationElement, operationIndex, policy, out var operationId, out error))
            {
                operations = null;
                return false;
            }

            if (!TryReadOperationName(operationElement, operationIndex, policy, out var operationName, out error))
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

    private static bool TryReadOperationId (
        JsonElement operationElement,
        int operationIndex,
        RequestSchemaPolicy policy,
        out string? operationId,
        out ExecutionError? error)
    {
        if (OperationContractReader.TryReadOperationId(operationElement, policy, out operationId, out var readError))
        {
            error = null;
            return true;
        }

        error = ValidateRequestParseErrorFactory.OperationStringProperty(operationIndex, "id", readError);
        return false;
    }

    private static bool TryReadOperationName (
        JsonElement operationElement,
        int operationIndex,
        RequestSchemaPolicy policy,
        out string? operationName,
        out ExecutionError? error)
    {
        if (OperationContractReader.TryReadOperationName(operationElement, policy, out operationName, out var readError))
        {
            error = null;
            return true;
        }

        error = ValidateRequestParseErrorFactory.OperationStringProperty(operationIndex, "op", readError);
        return false;
    }

    private static bool TryReadArgs (
        JsonElement operationElement,
        int operationIndex,
        out JsonElement args,
        out ExecutionError? error)
    {
        args = default;
        if (OperationContractReader.TryReadOperationArgs(operationElement, out var parsedArgs, out var readError))
        {
            args = parsedArgs.Clone();
            error = null;
            return true;
        }

        error = ValidateRequestParseErrorFactory.OperationArgs(operationIndex, readError);
        return false;
    }

    private static bool TryValidateOperationAlias (
        JsonElement operationElement,
        int operationIndex,
        out ExecutionError? error)
    {
        if (OperationContractReader.TryReadOperationAlias(operationElement, out _, out var readError))
        {
            error = null;
            return true;
        }

        error = ValidateRequestParseErrorFactory.OperationAlias(operationIndex, readError);
        return false;
    }

    private static bool TryValidateExpectation (
        JsonElement operationElement,
        int operationIndex,
        out ExecutionError? error)
    {
        if (ExpectationConstraintHelper.TryReadOptional(operationElement, out _, out var expectationError))
        {
            error = null;
            return true;
        }

        error = ValidateRequestParseErrorFactory.OperationExpectation(operationIndex, expectationError);
        return false;
    }
}