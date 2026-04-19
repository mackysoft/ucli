using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;

/// <summary> Parses request JSON into <see cref="ValidateRequest" /> values for static validation. </summary>
internal sealed class ValidateRequestJsonParser : IValidateRequestJsonParser
{
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

            List<ValidateRequestStep?>? parsedSteps = null;
            if (parsedContract.Steps is not null)
            {
                parsedSteps = new List<ValidateRequestStep?>(parsedContract.Steps.Count);
                foreach (var step in parsedContract.Steps)
                {
                    if (step is null)
                    {
                        parsedSteps.Add(null);
                        continue;
                    }

                    if (step.Kind == IpcRequestStepKind.Op
                        && (!step.Element.TryGetProperty("args", out var argsElement)
                            || argsElement.ValueKind != JsonValueKind.Object))
                    {
                        return ValidateRequestJsonParseResult.Failure(ExecutionError.InvalidArgument(
                            $"Step '{step.Id ?? string.Empty}' property 'args' is required."));
                    }

                    parsedSteps.Add(new ValidateRequestStep(
                        Kind: step.Kind,
                        StepId: step.Id,
                        Op: step.OperationName,
                        Element: step.Element));
                }
            }

            var parsedRequest = new ValidateRequest(
                ProtocolVersion: parsedContract.ProtocolVersion,
                RequestId: parsedContract.RequestId,
                Steps: parsedSteps);
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
            IpcRequestContractViolationKind.StepsMissing => ExecutionError.InvalidArgument(
                "Request property 'steps' is required."),
            IpcRequestContractViolationKind.StepsTypeMismatch => ExecutionError.InvalidArgument(
                "Request property 'steps' must be an array."),
            IpcRequestContractViolationKind.StepMustBeObject => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} must be an object."),
            IpcRequestContractViolationKind.StepKindMissing => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'kind' is required."),
            IpcRequestContractViolationKind.StepKindTypeMismatch => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'kind' must be a string."),
            IpcRequestContractViolationKind.StepKindEmptyOrWhitespace => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'kind' must not be empty."),
            IpcRequestContractViolationKind.StepKindOuterWhitespace => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'kind' must not contain leading or trailing whitespace."),
            IpcRequestContractViolationKind.StepKindUnsupported => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'kind' is unsupported: {violation.UnknownPropertyName}."),
            IpcRequestContractViolationKind.UnknownStepProperty => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} contains an unknown property: {violation.UnknownPropertyName}."),
            IpcRequestContractViolationKind.StepIdMissing => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'id' is required."),
            IpcRequestContractViolationKind.StepIdTypeMismatch => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'id' must be a string."),
            IpcRequestContractViolationKind.StepIdEmptyOrWhitespace => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'id' must not be empty."),
            IpcRequestContractViolationKind.StepIdOuterWhitespace => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'id' must not contain leading or trailing whitespace."),
            IpcRequestContractViolationKind.StepOpMissing => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'op' is required."),
            IpcRequestContractViolationKind.StepOpTypeMismatch => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'op' must be a string."),
            IpcRequestContractViolationKind.StepOpEmptyOrWhitespace => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'op' must not be empty."),
            IpcRequestContractViolationKind.StepOpOuterWhitespace => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'op' must not contain leading or trailing whitespace."),
            IpcRequestContractViolationKind.StepArgsMissing => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'args' is required."),
            IpcRequestContractViolationKind.StepArgsTypeMismatch => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'args' must be an object."),
            IpcRequestContractViolationKind.StepOnMissing => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'on' is required."),
            IpcRequestContractViolationKind.StepOnTypeMismatch => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'on' must be an object."),
            IpcRequestContractViolationKind.StepSelectMissing => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'select' is required."),
            IpcRequestContractViolationKind.StepSelectTypeMismatch => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'select' must be an object."),
            IpcRequestContractViolationKind.StepActionsMissing => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'actions' is required."),
            IpcRequestContractViolationKind.StepActionsTypeMismatch => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'actions' must be an array."),
            IpcRequestContractViolationKind.StepActionMustBeObject => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'actions' must contain only objects."),
            IpcRequestContractViolationKind.StepCommitMissing => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'commit' is required."),
            IpcRequestContractViolationKind.StepCommitTypeMismatch => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'commit' must be a string."),
            IpcRequestContractViolationKind.StepCommitEmptyOrWhitespace => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'commit' must not be empty."),
            IpcRequestContractViolationKind.StepCommitOuterWhitespace => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId ?? string.Empty}' property 'commit' must not contain leading or trailing whitespace."),
            IpcRequestContractViolationKind.DuplicatedStepId => ExecutionError.InvalidArgument(
                $"Step id is duplicated: {violation.DuplicatedStepId}."),
            _ => ExecutionError.InvalidArgument("Request JSON is invalid."),
        };
    }
}