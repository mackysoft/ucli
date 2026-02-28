using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
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
            CancellationToken cancellationToken,
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

            var policy = RequestSchemaPolicy.StrictExecute;
            var duplicatedOpIdDetector = new HashSet<string>(StringComparer.Ordinal);
            var operationIndex = 0;
            foreach (var operationElement in operationsElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (operationElement.ValueKind != JsonValueKind.Object)
                {
                    if (policy.RequireOperationObject)
                    {
                        error = ExecuteRequestNormalizationError.InvalidArgument($"Operation at index {operationIndex} must be an object.", null);
                        return false;
                    }

                    operationIndex++;
                    continue;
                }

                var unknownOperationProperty = OperationContractReader.FindUnknownOperationProperty(operationElement);
                if (unknownOperationProperty is not null)
                {
                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Operation at index {operationIndex} contains an unknown property: {unknownOperationProperty}.",
                        null);
                    return false;
                }

                if (!TryReadOperationId(operationElement, operationIndex, policy, out var operationId, out error))
                {
                    return false;
                }

                if (!duplicatedOpIdDetector.Add(operationId))
                {
                    error = ExecuteRequestNormalizationError.InvalidArgument($"Operation id is duplicated: {operationId}.", operationId);
                    return false;
                }

                if (!TryReadOperationName(operationElement, policy, operationId, out var operationName, out error))
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

        public static bool TryReadOperationId (
            JsonElement operationElement,
            int operationIndex,
            RequestSchemaPolicy policy,
            out string operationId,
            out ExecuteRequestNormalizationError? error)
        {
            operationId = string.Empty;
            if (OperationContractReader.TryReadOperationId(operationElement, policy, out var parsedOperationId, out var readError))
            {
                operationId = parsedOperationId!;
                error = null;
                return true;
            }

            error = ExecuteRequestNormalizationErrorFactory.OperationId(operationIndex, readError);
            return false;
        }

        public static bool TryReadOperationName (
            JsonElement operationElement,
            RequestSchemaPolicy policy,
            string operationId,
            out string operationName,
            out ExecuteRequestNormalizationError? error)
        {
            operationName = string.Empty;
            if (OperationContractReader.TryReadOperationName(operationElement, policy, out var parsedOperationName, out var readError))
            {
                operationName = parsedOperationName!;
                error = null;
                return true;
            }

            error = ExecuteRequestNormalizationErrorFactory.OperationName(operationId, readError);
            return false;
        }

        public static bool TryReadOperationArgs (
            JsonElement operationElement,
            string operationId,
            out JsonElement operationArgs,
            out ExecuteRequestNormalizationError? error)
        {
            operationArgs = default;
            if (OperationContractReader.TryReadOperationArgs(operationElement, out var parsedArgs, out var argsReadError))
            {
                operationArgs = parsedArgs;
                error = null;
                return true;
            }

            error = ExecuteRequestNormalizationErrorFactory.OperationArgs(operationId, argsReadError);
            return false;
        }

        public static bool TryReadOperationAs (
            JsonElement operationElement,
            string operationId,
            out string? operationAs,
            out ExecuteRequestNormalizationError? error)
        {
            operationAs = null;
            if (OperationContractReader.TryReadOperationAlias(operationElement, out var parsedOperationAs, out var readError))
            {
                operationAs = parsedOperationAs;
                error = null;
                return true;
            }

            error = ExecuteRequestNormalizationErrorFactory.OperationAlias(operationId, readError);
            return false;
        }

        public static bool TryReadExpectation (
            JsonElement operationElement,
            string operationId,
            out NormalizedExpectation? expectation,
            out ExecuteRequestNormalizationError? error)
        {
            expectation = null;
            if (!ExpectationConstraintHelper.TryReadOptional(operationElement, out var expectationConstraints, out var expectationReadError))
            {
                error = ExecuteRequestNormalizationErrorFactory.OperationExpectation(operationId, expectationReadError);
                return false;
            }

            if (!expectationConstraints.HasValue)
            {
                error = null;
                return true;
            }

            var parsedExpectation = expectationConstraints.Value;
            expectation = new NormalizedExpectation(
                NonNull: parsedExpectation.NonNull,
                Count: parsedExpectation.Count,
                Min: parsedExpectation.Min,
                Max: parsedExpectation.Max);
            error = null;
            return true;
        }
    }
}
