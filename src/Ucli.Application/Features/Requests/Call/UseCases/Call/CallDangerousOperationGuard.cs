using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;

/// <summary> Enforces the <c>--allowDangerous</c> policy for prepared <c>call</c> requests. </summary>
internal sealed class CallDangerousOperationGuard : ICallDangerousOperationGuard
{
    /// <inheritdoc />
    public ValidationError? Validate (
        PhaseExecutionPreparedRequest preparedRequest,
        bool allowDangerous)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);

        if (allowDangerous)
        {
            return null;
        }

        foreach (var step in preparedRequest.Request.Steps)
        {
            var stepPath = $"/steps/{step.StepIndex}";

            switch (step.Kind)
            {
                case IpcExecuteStepKind.Op:
                    var operationName = step.Op
                        ?? throw new InvalidOperationException(
                            $"Normalized operation step at '{stepPath}' has no operation name.");

                    if (TryFindDangerousOperation(operationName, preparedRequest.OperationsByName, out var operationDescriptor))
                    {
                        return new ValidationError(
                            OperationAuthorizationErrorCodes.OperationNotAllowed,
                            $"Operation '{operationDescriptor!.Name}' requires --allowDangerous.",
                            stepPath + "/op");
                    }

                    break;

                case IpcExecuteStepKind.Edit:
                    if (!RequestEditStepLowerPreviewBuilder.TryBuild(
                            step.EditContract,
                            preparedRequest.Request.AllowPlayMode,
                            out var operationNames,
                            out var errorMessage))
                    {
                        return new ValidationError(
                            ValidationErrorCodes.EditStepInvalid,
                            errorMessage,
                            stepPath);
                    }

                    for (var operationIndex = 0; operationIndex < operationNames.Count; operationIndex++)
                    {
                        var loweredOperationName = operationNames[operationIndex];
                        if (!TryFindDangerousOperation(loweredOperationName, preparedRequest.OperationsByName, out operationDescriptor))
                        {
                            continue;
                        }

                        return new ValidationError(
                            OperationAuthorizationErrorCodes.OperationNotAllowed,
                            $"Edit step requires dangerous operation '{operationDescriptor!.Name}'. Specify --allowDangerous to execute dangerous operations.",
                            stepPath);
                    }

                    break;
            }
        }

        return null;
    }

    private static bool TryFindDangerousOperation (
        string operationName,
        IReadOnlyDictionary<string, UcliOperationDescriptor> operationsByName,
        out UcliOperationDescriptor? operationDescriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operationsByName);

        if (operationsByName.TryGetValue(operationName, out operationDescriptor)
            && operationDescriptor.Policy == OperationPolicy.Dangerous)
        {
            return true;
        }

        operationDescriptor = null;
        return false;
    }
}
