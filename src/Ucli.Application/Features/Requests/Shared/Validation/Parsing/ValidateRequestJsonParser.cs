using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;

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

            if (!IpcExecuteArgumentsContractReader.TryRead(
                argumentsObject: document.RootElement,
                profile: IpcExecuteArgumentsContractReadProfile.PermissivePreflight,
                argumentsContract: out var parsedArguments,
                error: out var readError))
            {
                return ValidateRequestJsonParseResult.Failure(MapReadError(readError));
            }

            List<ValidateRequestStep?>? parsedSteps = null;
            if (parsedArguments.Steps is not null)
            {
                parsedSteps = new List<ValidateRequestStep?>(parsedArguments.Steps.Count);
                foreach (var step in parsedArguments.Steps)
                {
                    if (step is null)
                    {
                        parsedSteps.Add(null);
                        continue;
                    }

                    if (step.Kind == IpcExecuteStepKind.Op
                        && (!step.Element.TryGetProperty("args", out var argsElement)
                            || argsElement.ValueKind != JsonValueKind.Object))
                    {
                        return ValidateRequestJsonParseResult.Failure(ExecutionError.InvalidArgument(
                            $"Step '{step.Id}' property 'args' is required."));
                    }

                    parsedSteps.Add(new ValidateRequestStep(
                        Kind: step.Kind,
                        StepId: step.Id,
                        Op: step.OperationName,
                        Element: step.Element));
                }
            }

            var parsedRequest = new ValidateRequest(
                ProtocolVersion: parsedArguments.ProtocolVersion,
                Steps: parsedSteps);
            return ValidateRequestJsonParseResult.Success(parsedRequest);
        }
        catch (JsonException exception)
        {
            return ValidateRequestJsonParseResult.Failure(ExecutionError.InvalidArgument(
                $"Request JSON is invalid. {exception.Message}"));
        }
    }

    private static ExecutionError MapReadError (in IpcExecuteArgumentsContractReadError readError)
    {
        var violation = IpcExecuteArgumentsContractViolationClassifier.Classify(readError);
        return violation.Kind switch
        {
            IpcExecuteArgumentsContractViolationKind.ArgumentsMustBeObject => ExecutionError.InvalidArgument(
                "Request JSON root must be an object."),
            IpcExecuteArgumentsContractViolationKind.UnknownArgumentsProperty => ExecutionError.InvalidArgument(
                $"Request contains an unknown property: {violation.UnknownPropertyName}."),
            IpcExecuteArgumentsContractViolationKind.ProtocolVersionMissing => ExecutionError.InvalidArgument(
                "Request property 'protocolVersion' is required."),
            IpcExecuteArgumentsContractViolationKind.ProtocolVersionTypeMismatch => ExecutionError.InvalidArgument(
                "Request property 'protocolVersion' must be an integer."),
            IpcExecuteArgumentsContractViolationKind.StepsMissing => ExecutionError.InvalidArgument(
                "Request property 'steps' is required."),
            IpcExecuteArgumentsContractViolationKind.StepsTypeMismatch => ExecutionError.InvalidArgument(
                "Request property 'steps' must be an array."),
            IpcExecuteArgumentsContractViolationKind.StepMustBeObject => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} must be an object."),
            IpcExecuteArgumentsContractViolationKind.StepKindMissing => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'kind' is required."),
            IpcExecuteArgumentsContractViolationKind.StepKindTypeMismatch => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'kind' must be a string."),
            IpcExecuteArgumentsContractViolationKind.StepKindEmptyOrWhitespace => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'kind' must not be empty."),
            IpcExecuteArgumentsContractViolationKind.StepKindOuterWhitespace => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'kind' must not contain leading or trailing whitespace."),
            IpcExecuteArgumentsContractViolationKind.StepKindUnsupported => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'kind' is unsupported: {violation.UnknownPropertyName}."),
            IpcExecuteArgumentsContractViolationKind.UnknownStepProperty => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} contains an unknown property: {violation.UnknownPropertyName}."),
            IpcExecuteArgumentsContractViolationKind.StepIdMissing => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'id' is required."),
            IpcExecuteArgumentsContractViolationKind.StepIdTypeMismatch => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'id' must be a string."),
            IpcExecuteArgumentsContractViolationKind.StepIdEmptyOrWhitespace => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'id' must not be empty."),
            IpcExecuteArgumentsContractViolationKind.StepIdOuterWhitespace => ExecutionError.InvalidArgument(
                $"Step at index {violation.StepIndex} property 'id' must not contain leading or trailing whitespace."),
            IpcExecuteArgumentsContractViolationKind.StepOpMissing => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'op' is required."),
            IpcExecuteArgumentsContractViolationKind.StepOpTypeMismatch => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'op' must be a string."),
            IpcExecuteArgumentsContractViolationKind.StepOpEmptyOrWhitespace => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'op' must not be empty."),
            IpcExecuteArgumentsContractViolationKind.StepOpOuterWhitespace => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'op' must not contain leading or trailing whitespace."),
            IpcExecuteArgumentsContractViolationKind.StepArgsMissing => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'args' is required."),
            IpcExecuteArgumentsContractViolationKind.StepArgsTypeMismatch => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'args' must be an object."),
            IpcExecuteArgumentsContractViolationKind.StepOnMissing => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'on' is required."),
            IpcExecuteArgumentsContractViolationKind.StepOnTypeMismatch => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'on' must be an object."),
            IpcExecuteArgumentsContractViolationKind.StepSelectMissing => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'select' is required."),
            IpcExecuteArgumentsContractViolationKind.StepSelectTypeMismatch => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'select' must be an object."),
            IpcExecuteArgumentsContractViolationKind.StepActionsMissing => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'actions' is required."),
            IpcExecuteArgumentsContractViolationKind.StepActionsTypeMismatch => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'actions' must be an array."),
            IpcExecuteArgumentsContractViolationKind.StepActionMustBeObject => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'actions' must contain only objects."),
            IpcExecuteArgumentsContractViolationKind.StepCommitMissing => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'commit' is required."),
            IpcExecuteArgumentsContractViolationKind.StepCommitTypeMismatch => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'commit' must be a string."),
            IpcExecuteArgumentsContractViolationKind.StepCommitEmptyOrWhitespace => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'commit' must not be empty."),
            IpcExecuteArgumentsContractViolationKind.StepCommitOuterWhitespace => ExecutionError.InvalidArgument(
                $"Step '{violation.StepId}' property 'commit' must not contain leading or trailing whitespace."),
            IpcExecuteArgumentsContractViolationKind.DuplicatedStepId => ExecutionError.InvalidArgument(
                $"Step id is duplicated: {violation.DuplicatedStepId}."),
            _ => ExecutionError.InvalidArgument("Request JSON is invalid."),
        };
    }
}
