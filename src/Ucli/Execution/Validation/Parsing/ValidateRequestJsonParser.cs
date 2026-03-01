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
        return readError.Kind switch
        {
            IpcRequestContractReadErrorKind.RequestMustBeObject => ExecutionError.InvalidArgument(
                "Request JSON root must be an object."),
            IpcRequestContractReadErrorKind.UnknownRequestProperty => ExecutionError.InvalidArgument(
                $"Request contains an unknown property: {readError.UnknownPropertyName}."),
            IpcRequestContractReadErrorKind.ProtocolVersionMissing => ExecutionError.InvalidArgument(
                "Request property 'protocolVersion' is required."),
            IpcRequestContractReadErrorKind.ProtocolVersionTypeMismatch => ExecutionError.InvalidArgument(
                "Request property 'protocolVersion' must be an integer."),
            IpcRequestContractReadErrorKind.RequestIdContractViolation => ExecutionError.InvalidArgument(
                "Request property 'requestId' is invalid."),
            IpcRequestContractReadErrorKind.RequestIdFormatMismatch => ExecutionError.InvalidArgument(
                "Request property 'requestId' must be UUID format 'D'."),
            IpcRequestContractReadErrorKind.OperationsMissing => ExecutionError.InvalidArgument(
                "Request property 'ops' is required."),
            IpcRequestContractReadErrorKind.OperationsTypeMismatch => ExecutionError.InvalidArgument(
                "Request property 'ops' must be an array."),
            IpcRequestContractReadErrorKind.OperationMustBeObject => ValidateRequestParseErrorFactory.OperationMustBeObject(readError.OperationIndex),
            IpcRequestContractReadErrorKind.UnknownOperationProperty => ValidateRequestParseErrorFactory.UnknownOperationProperty(
                readError.OperationIndex,
                readError.UnknownPropertyName ?? string.Empty),
            IpcRequestContractReadErrorKind.OperationIdContractViolation => ValidateRequestParseErrorFactory.OperationStringProperty(
                readError.OperationIndex,
                "id",
                readError.JsonStringReadError),
            IpcRequestContractReadErrorKind.OperationNameContractViolation => ValidateRequestParseErrorFactory.OperationStringProperty(
                readError.OperationIndex,
                "op",
                readError.JsonStringReadError),
            IpcRequestContractReadErrorKind.OperationArgsContractViolation => ValidateRequestParseErrorFactory.OperationArgs(
                readError.OperationIndex,
                readError.OperationObjectReadErrorKind),
            IpcRequestContractReadErrorKind.OperationAliasContractViolation => ValidateRequestParseErrorFactory.OperationAlias(
                readError.OperationIndex,
                readError.JsonStringReadError),
            IpcRequestContractReadErrorKind.OperationExpectationContractViolation => ValidateRequestParseErrorFactory.OperationExpectation(
                readError.OperationIndex,
                readError.ExpectationReadError),
            IpcRequestContractReadErrorKind.DuplicatedOperationId => ExecutionError.InvalidArgument(
                $"Operation id is duplicated: {readError.DuplicatedOperationId}."),
            _ => ExecutionError.InvalidArgument("Request JSON is invalid."),
        };
    }
}