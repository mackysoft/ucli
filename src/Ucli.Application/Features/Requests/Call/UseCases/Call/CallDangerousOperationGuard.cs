using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Text;

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

        if (allowDangerous
            || preparedRequest.Request.Steps == null)
        {
            return null;
        }

        foreach (var step in preparedRequest.Request.Steps)
        {
            if (step == null)
            {
                continue;
            }

            switch (step.Kind)
            {
                case IpcRequestStepKind.Op:
                    if (!StringValueNormalizer.TryTrimToNonEmpty(step.Op, out var operationName))
                    {
                        continue;
                    }

                    if (TryFindDangerousOperation(operationName, preparedRequest.OperationsByName, out var operationDescriptor))
                    {
                        return new ValidationError(
                            OperationAuthorizationErrorCodes.OperationNotAllowed,
                            $"Step '{step.StepId ?? string.Empty}' requires dangerous operation '{operationDescriptor!.Name}'. Specify --allowDangerous to execute dangerous operations.",
                            step.StepId);
                    }

                    break;

                case IpcRequestStepKind.Edit:
                    if (!RequestEditStepLowerPreviewBuilder.TryBuild(
                            step.Element,
                            preparedRequest.Request.AllowPlayMode,
                            out var operationNames,
                            out var errorMessage))
                    {
                        return new ValidationError(
                            ValidationErrorCodes.EditStepInvalid,
                            errorMessage,
                            step.StepId);
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
                            $"Edit step '{step.StepId ?? string.Empty}' requires dangerous operation '{operationDescriptor!.Name}'. Specify --allowDangerous to execute dangerous operations.",
                            step.StepId);
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
