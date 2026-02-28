using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Validation;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Reads and validates execute-request operation entries. </summary>
    internal static class ExecuteRequestOperationReader
    {
        /// <summary> Reads one required operation list. </summary>
        public static bool TryReadOperations (
            JsonElement requestArguments,
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

            var duplicatedOpIdDetector = new HashSet<string>(StringComparer.Ordinal);
            var operationIndex = 0;
            foreach (var operationElement in operationsElement.EnumerateArray())
            {
                if (!ExecuteRequestOperationElementReader.TryReadOperation(
                    operationElement,
                    operationIndex,
                    RequestSchemaPolicy.StrictExecute,
                    out var parsedOperation,
                    out error))
                {
                    return false;
                }

                if (parsedOperation is null)
                {
                    operationIndex++;
                    continue;
                }

                if (!duplicatedOpIdDetector.Add(parsedOperation.Id))
                {
                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Operation id is duplicated: {parsedOperation.Id}.",
                        parsedOperation.Id);
                    return false;
                }

                operations.Add(parsedOperation);
                operationIndex++;
            }

            return true;
        }
    }
}
