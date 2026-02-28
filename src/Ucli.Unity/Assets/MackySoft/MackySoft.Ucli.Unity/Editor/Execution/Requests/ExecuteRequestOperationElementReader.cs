using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Validation;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Reads and validates one execute-request operation entry. </summary>
    internal static class ExecuteRequestOperationElementReader
    {
        /// <summary> Reads one operation entry and converts it into a normalized model. </summary>
        public static bool TryReadOperation (
            JsonElement operationElement,
            int operationIndex,
            in RequestSchemaPolicy policy,
            out NormalizedOperation? operation,
            out ExecuteRequestNormalizationError? error)
        {
            operation = null;
            error = null;

            if (operationElement.ValueKind != JsonValueKind.Object)
            {
                if (policy.RequireOperationObject)
                {
                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Operation at index {operationIndex} must be an object.",
                        null);
                    return false;
                }

                return true;
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

            if (!TryReadOperationName(operationElement, policy, operationId, out var operationName, out error))
            {
                return false;
            }

            if (!TryReadOperationArgs(operationElement, operationId, out var operationArgs, out error))
            {
                return false;
            }

            if (!TryReadOperationAlias(operationElement, operationId, out var operationAs, out error))
            {
                return false;
            }

            if (!TryReadExpectation(operationElement, operationId, out var expectation, out error))
            {
                return false;
            }

            operation = new NormalizedOperation(
                Id: operationId,
                Op: operationName,
                Args: operationArgs.Clone(),
                As: operationAs,
                Expect: expectation);
            return true;
        }

        private static bool TryReadOperationId (
            JsonElement operationElement,
            int operationIndex,
            in RequestSchemaPolicy policy,
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

        private static bool TryReadOperationName (
            JsonElement operationElement,
            in RequestSchemaPolicy policy,
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

        private static bool TryReadOperationArgs (
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

        private static bool TryReadOperationAlias (
            JsonElement operationElement,
            string operationId,
            out string? operationAlias,
            out ExecuteRequestNormalizationError? error)
        {
            operationAlias = null;
            if (OperationContractReader.TryReadOperationAlias(operationElement, out var parsedOperationAlias, out var readError))
            {
                operationAlias = parsedOperationAlias;
                error = null;
                return true;
            }

            error = ExecuteRequestNormalizationErrorFactory.OperationAlias(operationId, readError);
            return false;
        }

        private static bool TryReadExpectation (
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
