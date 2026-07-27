using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;

/// <summary>Parses the normalized execute-arguments DTO for static product validation.</summary>
internal sealed class ValidateRequestJsonParser : IValidateRequestJsonParser
{
    public ValidateRequestJsonParseResult Parse (string requestJson)
    {
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return Failure("Request JSON must not be empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(requestJson);
            if (!IpcExecuteArgumentsContractReader.TryRead(
                    document.RootElement,
                    out var parsedArguments,
                    out var readError))
            {
                return Failure(readError.Message);
            }

            var parsedSteps = new List<ValidateRequestStep>(parsedArguments.Steps.Count);
            for (var stepIndex = 0; stepIndex < parsedArguments.Steps.Count; stepIndex++)
            {
                var step = parsedArguments.Steps[stepIndex];
                parsedSteps.Add(new ValidateRequestStep(
                    step.Kind,
                    stepIndex,
                    step.OperationName,
                    step.OperationArgs)
                {
                    EditContract = step.EditContract,
                });
            }

            return ValidateRequestJsonParseResult.Success(new ValidateRequest(
                parsedArguments.ProtocolVersion,
                parsedSteps));
        }
        catch (JsonException exception)
        {
            return Failure($"Request JSON is invalid. {exception.Message}");
        }
    }

    private static ValidateRequestJsonParseResult Failure (string message)
    {
        return ValidateRequestJsonParseResult.Failure(ExecutionError.InvalidArgument(message));
    }
}
