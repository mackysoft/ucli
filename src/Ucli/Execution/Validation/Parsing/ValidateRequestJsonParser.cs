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

            if (!IpcRequestContractReader.TryRead(
                requestObject: document.RootElement,
                profile: IpcRequestContractReadProfile.PermissivePreflight,
                requestContract: out var parsedContract,
                error: out var readError))
            {
                return ValidateRequestJsonParseResult.Failure(MapReadError(readError));
            }

            List<ValidateRequestOperation?>? parsedOperations = null;
            if (parsedContract.Operations is not null)
            {
                parsedOperations = new List<ValidateRequestOperation?>(parsedContract.Operations.Count);
                foreach (var operation in parsedContract.Operations)
                {
                    if (operation is null)
                    {
                        parsedOperations.Add(null);
                        continue;
                    }

                    parsedOperations.Add(new ValidateRequestOperation(
                        OpId: operation.Id,
                        Op: operation.Name,
                        Args: operation.Args));
                }
            }

            var parsedRequest = new ValidateRequest(
                ProtocolVersion: parsedContract.ProtocolVersion,
                RequestId: parsedContract.RequestId,
                Ops: parsedOperations);
            return ValidateRequestJsonParseResult.Success(parsedRequest);
        }
        catch (JsonException exception)
        {
            return ValidateRequestJsonParseResult.Failure(ExecutionError.InvalidArgument(
                $"Request JSON is invalid. {exception.Message}"));
        }
    }

    private static ExecutionError MapReadError (in IpcRequestContractReadError readError)
    {
        var violation = IpcRequestContractViolationClassifier.Classify(readError);
        return violation.Kind switch
        {
            IpcRequestContractViolationKind.RequestMustBeObject => ExecutionError.InvalidArgument(
                "Request JSON root must be an object."),
            IpcRequestContractViolationKind.UnknownRequestProperty => ExecutionError.InvalidArgument(
                $"Request contains an unknown property: {violation.UnknownPropertyName}."),
            IpcRequestContractViolationKind.ProtocolVersionMissing => ExecutionError.InvalidArgument(
                "Request property 'protocolVersion' is required."),
            IpcRequestContractViolationKind.ProtocolVersionTypeMismatch => ExecutionError.InvalidArgument(
                "Request property 'protocolVersion' must be an integer."),
            IpcRequestContractViolationKind.RequestIdMissing => ExecutionError.InvalidArgument(
                "Request property 'requestId' is invalid."),
            IpcRequestContractViolationKind.RequestIdTypeMismatch => ExecutionError.InvalidArgument(
                "Request property 'requestId' is invalid."),
            IpcRequestContractViolationKind.RequestIdEmptyOrWhitespace => ExecutionError.InvalidArgument(
                "Request property 'requestId' is invalid."),
            IpcRequestContractViolationKind.RequestIdOuterWhitespace => ExecutionError.InvalidArgument(
                "Request property 'requestId' is invalid."),
            IpcRequestContractViolationKind.RequestIdFormatMismatch => ExecutionError.InvalidArgument(
                "Request property 'requestId' must be UUID format 'D'."),
            IpcRequestContractViolationKind.OperationsMissing => ExecutionError.InvalidArgument(
                "Request property 'ops' is required."),
            IpcRequestContractViolationKind.OperationsTypeMismatch => ExecutionError.InvalidArgument(
                "Request property 'ops' must be an array."),
            IpcRequestContractViolationKind.OperationMustBeObject => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} must be an object."),
            IpcRequestContractViolationKind.UnknownOperationProperty => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} contains an unknown property: {violation.UnknownPropertyName ?? string.Empty}."),
            IpcRequestContractViolationKind.OperationIdMissing => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'id' is required."),
            IpcRequestContractViolationKind.OperationIdTypeMismatch => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'id' must be a string when specified."),
            IpcRequestContractViolationKind.OperationIdEmptyOrWhitespace => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'id' must not be empty."),
            IpcRequestContractViolationKind.OperationIdOuterWhitespace => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'id' must not contain leading or trailing whitespace."),
            IpcRequestContractViolationKind.OperationNameMissing => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'op' is required."),
            IpcRequestContractViolationKind.OperationNameTypeMismatch => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'op' must be a string when specified."),
            IpcRequestContractViolationKind.OperationNameEmptyOrWhitespace => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'op' must not be empty."),
            IpcRequestContractViolationKind.OperationNameOuterWhitespace => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'op' must not contain leading or trailing whitespace."),
            IpcRequestContractViolationKind.OperationArgsMissing => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'args' is required."),
            IpcRequestContractViolationKind.OperationArgsTypeMismatch => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'args' must be an object."),
            IpcRequestContractViolationKind.OperationAliasTypeMismatch => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'as' must be a string when specified."),
            IpcRequestContractViolationKind.OperationAliasEmptyOrWhitespace => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'as' must not be empty or contain outer whitespace."),
            IpcRequestContractViolationKind.OperationAliasOuterWhitespace => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'as' must not be empty or contain outer whitespace."),
            IpcRequestContractViolationKind.ExpectationMustBeObject => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'expect' must be an object when specified."),
            IpcRequestContractViolationKind.ExpectationContainsUnknownProperty => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'expect' contains an unknown property: {violation.UnknownPropertyName}."),
            IpcRequestContractViolationKind.ExpectationMustContainAtLeastOneConstraint => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'expect' must contain at least one constraint."),
            IpcRequestContractViolationKind.ExpectationBooleanConstraintMustBeBoolean => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property '{violation.PropertyPath}' must be a boolean."),
            IpcRequestContractViolationKind.ExpectationIntegerConstraintMustBeInteger => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property '{violation.PropertyPath}' must be an integer."),
            IpcRequestContractViolationKind.ExpectationIntegerConstraintMustBeNonNegative => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property '{violation.PropertyPath}' must be greater than or equal to 0."),
            IpcRequestContractViolationKind.ExpectationCountCannotCombineWithMinOrMax => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'expect' cannot combine 'count' with 'min' or 'max'."),
            IpcRequestContractViolationKind.ExpectationMinMustBeLessThanOrEqualToMax => ExecutionError.InvalidArgument(
                $"Operation at index {violation.OperationIndex} property 'expect' requires 'min' to be less than or equal to 'max'."),
            IpcRequestContractViolationKind.DuplicatedOperationId => ExecutionError.InvalidArgument(
                $"Operation id is duplicated: {violation.DuplicatedOperationId}."),
            _ => ExecutionError.InvalidArgument("Request JSON is invalid."),
        };
    }
}