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

        var parsedOperations = new List<ValidateRequestOperation?>();
        var operationIndex = 0;
        foreach (var operationElement in operationsElement.EnumerateArray())
        {
            if (!ValidateRequestOperationElementReader.TryReadOperation(
                operationElement,
                operationIndex,
                RequestSchemaPolicy.PermissivePreflight,
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
        return true;
    }
}