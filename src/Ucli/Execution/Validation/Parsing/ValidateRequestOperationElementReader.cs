using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;

namespace MackySoft.Ucli.Execution;

/// <summary> Reads one operation contract entry from preflight request JSON payloads. </summary>
internal static class ValidateRequestOperationElementReader
{
    /// <summary> Reads one operation entry and validates strict preflight constraints. </summary>
    /// <param name="operationElement"> The source operation element. </param>
    /// <param name="operationIndex"> The operation index in request payload. </param>
    /// <param name="policy"> The operation schema policy. </param>
    /// <param name="operation"> Parsed operation, or <see langword="null" /> when policy allows non-object entries. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when operation element is valid; otherwise <see langword="false" />. </returns>
    public static bool TryReadOperation (
        JsonElement operationElement,
        int operationIndex,
        in RequestSchemaPolicy policy,
        out ValidateRequestOperation? operation,
        out ExecutionError? error)
    {
        operation = null;
        error = null;

        if (operationElement.ValueKind != JsonValueKind.Object)
        {
            if (policy.RequireOperationObject)
            {
                error = ValidateRequestParseErrorFactory.OperationMustBeObject(operationIndex);
                return false;
            }

            return true;
        }

        var unknownOperationProperty = OperationContractReader.FindUnknownOperationProperty(operationElement);
        if (unknownOperationProperty is not null)
        {
            error = ValidateRequestParseErrorFactory.UnknownOperationProperty(operationIndex, unknownOperationProperty);
            return false;
        }

        if (!TryReadOperationId(operationElement, operationIndex, policy, out var operationId, out error))
        {
            return false;
        }

        if (!TryReadOperationName(operationElement, operationIndex, policy, out var operationName, out error))
        {
            return false;
        }

        if (!TryReadArgs(operationElement, operationIndex, out var args, out error))
        {
            return false;
        }

        if (!TryValidateOperationAlias(operationElement, operationIndex, out error))
        {
            return false;
        }

        if (!TryValidateExpectation(operationElement, operationIndex, out error))
        {
            return false;
        }

        operation = new ValidateRequestOperation(
            OpId: operationId,
            Op: operationName,
            Args: args);
        return true;
    }

    private static bool TryReadOperationId (
        JsonElement operationElement,
        int operationIndex,
        in RequestSchemaPolicy policy,
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
        in RequestSchemaPolicy policy,
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