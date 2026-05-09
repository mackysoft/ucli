using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Performs static request validation for protocol, structure, and operation authorization. </summary>
internal sealed class RequestStaticValidator : IRequestStaticValidator
{
    private readonly IOperationAuthorizationService operationAuthorizationService;

    /// <summary> Initializes a new instance of the <see cref="RequestStaticValidator" /> class. </summary>
    /// <param name="operationAuthorizationService"> The operation authorization dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationAuthorizationService" /> is <see langword="null" />. </exception>
    public RequestStaticValidator (
        IOperationAuthorizationService operationAuthorizationService)
    {
        this.operationAuthorizationService = operationAuthorizationService ?? throw new ArgumentNullException(nameof(operationAuthorizationService));
    }

    /// <inheritdoc />
    public async ValueTask<ValidationResult> ValidateAsync (
        ValidateRequest request,
        RequestStaticValidationCatalog catalog,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<ValidationError>();
        if (request.ProtocolVersion != IpcProtocol.CurrentVersion)
        {
            errors.Add(new ValidationError(
                Code: IpcProtocolErrorCodes.ProtocolVersionMismatch,
                Message: $"protocolVersion must be {IpcProtocol.CurrentVersion}. Actual: {request.ProtocolVersion}.",
                OpId: null));
        }

        if (string.IsNullOrWhiteSpace(request.RequestId)
            || !Guid.TryParseExact(request.RequestId, "D", out _))
        {
            errors.Add(new ValidationError(
                Code: ValidationErrorCodes.RequestIdInvalid,
                Message: "requestId must be UUID format 'D'.",
                OpId: null));
        }

        if (request.Steps is null)
        {
            errors.Add(new ValidationError(
                Code: ValidationErrorCodes.StepsRequired,
                Message: "steps is required.",
                OpId: null));
            return new ValidationResult(errors);
        }

        if (request.Steps.Count == 0)
        {
            return errors.Count == 0
                ? ValidationResult.Success()
                : new ValidationResult(errors);
        }

        Dictionary<string, UcliOperationDescriptor>? operationsByName = null;
        if (catalog.IsAvailable)
        {
            operationsByName = new Dictionary<string, UcliOperationDescriptor>(catalog.Operations.Count, StringComparer.Ordinal);
            for (var i = 0; i < catalog.Operations.Count; i++)
            {
                var operationDescriptor = catalog.Operations[i];
                cancellationToken.ThrowIfCancellationRequested();
                operationsByName[operationDescriptor.Name] = operationDescriptor;
            }
        }

        var authorizationCache = new Dictionary<string, OperationAuthorizationResult>(StringComparer.Ordinal);
        var usedStepIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in request.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (step is null)
            {
                errors.Add(new ValidationError(
                    Code: ValidationErrorCodes.StepIdRequired,
                    Message: "step.id is required.",
                    OpId: null));
                errors.Add(new ValidationError(
                    Code: ValidationErrorCodes.StepKindRequired,
                    Message: "step.kind is required.",
                    OpId: null));
                continue;
            }

            if (!StringValueNormalizer.TryTrimToNonEmpty(step.StepId, out var normalizedStepId))
            {
                errors.Add(new ValidationError(
                    Code: ValidationErrorCodes.StepIdRequired,
                    Message: "step.id is required.",
                    OpId: null));
            }
            else if (!usedStepIds.Add(normalizedStepId))
            {
                errors.Add(new ValidationError(
                    Code: ValidationErrorCodes.StepIdDuplicated,
                    Message: $"step.id '{normalizedStepId}' is duplicated.",
                    OpId: normalizedStepId));
            }

            if (step.Kind is null)
            {
                errors.Add(new ValidationError(
                    Code: ValidationErrorCodes.StepKindRequired,
                    Message: "step.kind is required.",
                    OpId: normalizedStepId));
                continue;
            }

            switch (step.Kind)
            {
                case IpcRequestStepKind.Op:
                    if (!StringValueNormalizer.TryTrimToNonEmpty(step.Op, out var normalizedOperationName))
                    {
                        errors.Add(new ValidationError(
                            Code: ValidationErrorCodes.OperationNameRequired,
                            Message: "step.op is required.",
                            OpId: normalizedStepId));
                        continue;
                    }

                    if ((operationsByName != null)
                        && operationsByName.TryGetValue(normalizedOperationName, out var operationDescriptor))
                    {
                        var argsValidationFailure = TryValidateOperationArgs(
                            step,
                            normalizedStepId,
                            operationDescriptor,
                            errors);
                        if (argsValidationFailure is not null)
                        {
                            return argsValidationFailure;
                        }
                    }

                    if (operationsByName != null)
                    {
                        await ValidateReferencedOperationAsync(
                                normalizedOperationName,
                                normalizedStepId,
                                isImplicitEditOperation: false,
                                operationsByName,
                                authorizationCache,
                                config,
                                errors,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    break;

                case IpcRequestStepKind.Edit:
                    if (!RequestEditStepLowerPreviewBuilder.TryBuild(
                        step.Element,
                        out var operationNames,
                        out var errorMessage))
                    {
                        errors.Add(new ValidationError(
                            Code: ValidationErrorCodes.EditStepInvalid,
                            Message: errorMessage,
                            OpId: normalizedStepId));
                        continue;
                    }

                    if (operationsByName == null)
                    {
                        break;
                    }

                    var uniqueOperationNames = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var operationName in operationNames)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!uniqueOperationNames.Add(operationName))
                        {
                            continue;
                        }

                        await ValidateReferencedOperationAsync(
                                operationName,
                                normalizedStepId,
                                isImplicitEditOperation: true,
                                operationsByName,
                                authorizationCache,
                                config,
                                errors,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    break;

                default:
                    errors.Add(new ValidationError(
                        Code: ValidationErrorCodes.StepKindInvalid,
                        Message: $"step.kind '{step.Kind}' is unsupported.",
                        OpId: normalizedStepId));
                    break;
            }
        }

        return new ValidationResult(errors);
    }

    private static ValidationResult? TryValidateOperationArgs (
        ValidateRequestStep step,
        string? stepId,
        UcliOperationDescriptor operationDescriptor,
        ICollection<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(operationDescriptor);
        ArgumentNullException.ThrowIfNull(errors);

        if (!step.Element.TryGetProperty("args", out var argsElement)
            || argsElement.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            errors.Add(new ValidationError(
                Code: ValidationErrorCodes.OperationArgsInvalid,
                Message: $"Step '{stepId ?? string.Empty}' property 'args' must be an object.",
                OpId: stepId));
            return null;
        }

        if (OperationArgsStaticSchemaValidator.TryValidate(
            operationDescriptor.ArgsSchemaJson,
            argsElement,
            out var schemaInvalid,
            out var error))
        {
            return null;
        }

        if (schemaInvalid)
        {
            return ValidationResult.Failure(ExecutionError.InternalError(
                $"Static validation could not validate args for operation '{operationDescriptor.Name}'. {error}"));
        }

        errors.Add(new ValidationError(
            Code: ValidationErrorCodes.OperationArgsInvalid,
            Message: $"Step '{stepId ?? string.Empty}' args for operation '{operationDescriptor.Name}' are invalid. {error}",
            OpId: stepId));
        return null;
    }

    private async ValueTask ValidateReferencedOperationAsync (
        string operationName,
        string? stepId,
        bool isImplicitEditOperation,
        IReadOnlyDictionary<string, UcliOperationDescriptor> operationsByName,
        IDictionary<string, OperationAuthorizationResult> authorizationCache,
        UcliConfig config,
        ICollection<ValidationError> errors,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operationsByName);
        ArgumentNullException.ThrowIfNull(authorizationCache);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(errors);

        if (!operationsByName.TryGetValue(operationName, out var descriptor))
        {
            var message = isImplicitEditOperation
                ? $"Edit step '{stepId ?? string.Empty}' requires operation '{operationName}', but it is not registered."
                : $"Operation '{operationName}' is not registered.";
            errors.Add(new ValidationError(
                Code: ValidationErrorCodes.OperationNotFound,
                Message: message,
                OpId: stepId));
            return;
        }

        if (!authorizationCache.TryGetValue(operationName, out var authorizationResult))
        {
            authorizationResult = await operationAuthorizationService
                .AuthorizeAsync(descriptor, config, cancellationToken)
                .ConfigureAwait(false);
            authorizationCache[operationName] = authorizationResult;
        }

        if (!authorizationResult.IsAllowed)
        {
            var message = isImplicitEditOperation
                ? $"Edit step '{stepId ?? string.Empty}' requires operation '{operationName}'. {authorizationResult.Message}"
                : authorizationResult.Message;
            errors.Add(new ValidationError(
                Code: authorizationResult.ErrorCode ?? OperationAuthorizationErrorCodes.OperationNotAllowed,
            Message: message,
            OpId: stepId));
        }
    }
}
