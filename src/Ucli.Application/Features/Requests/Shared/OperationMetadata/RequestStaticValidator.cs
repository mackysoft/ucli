using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary>Performs static request validation for protocol and operation-specific product semantics.</summary>
internal sealed class RequestStaticValidator : IRequestStaticValidator
{
    private readonly IOperationAuthorizationService operationAuthorizationService;

    /// <summary>Initializes a new instance of the <see cref="RequestStaticValidator" /> class.</summary>
    /// <param name="operationAuthorizationService">The operation authorization dependency.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operationAuthorizationService" /> is <see langword="null" />.</exception>
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
        ArgumentNullException.ThrowIfNull(request.Steps);

        var errors = new List<ValidationError>();
        if (request.ProtocolVersion != IpcProtocol.CurrentVersion)
        {
            errors.Add(new ValidationError(
                IpcProtocolErrorCodes.ProtocolVersionMismatch,
                $"protocolVersion must be {IpcProtocol.CurrentVersion}. Actual: {request.ProtocolVersion}.",
                "/protocolVersion"));
        }

        if (request.Steps.Count == 0)
        {
            return errors.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Invalid(errors);
        }

        Dictionary<string, UcliOperationDescriptor>? operationsByName = null;
        Dictionary<string, UcliOperationAuthorizationDescriptor>? authorizationOperationsByName =
            null;
        if (catalog.IsAvailable)
        {
            operationsByName = new Dictionary<string, UcliOperationDescriptor>(
                catalog.Operations.Count,
                StringComparer.Ordinal);
            for (var i = 0; i < catalog.Operations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var operationDescriptor = catalog.Operations[i];
                operationsByName[operationDescriptor.Name] = operationDescriptor;
            }

            authorizationOperationsByName =
                new Dictionary<string, UcliOperationAuthorizationDescriptor>(
                    catalog.AuthorizationOperations.Count,
                    StringComparer.Ordinal);
            for (var i = 0; i < catalog.AuthorizationOperations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var operation = catalog.AuthorizationOperations[i];
                authorizationOperationsByName[operation.Name] = operation;
            }
        }

        var authorizationCache = new Dictionary<string, OperationAuthorizationResult>(StringComparer.Ordinal);
        for (var i = 0; i < request.Steps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = request.Steps[i];
            var stepPath = $"/steps/{step.StepIndex}";

            switch (step.Kind)
            {
                case IpcExecuteStepKind.Op:
                    var operationName = step.Op
                        ?? throw new InvalidOperationException(
                            $"Normalized operation step at '{stepPath}' has no operation name.");
                    var operationPath = stepPath + "/op";

                    if (operationsByName != null
                        && operationsByName.TryGetValue(operationName, out var operationDescriptor))
                    {
                        var argsValidationFailure = OperationArgsSchemaEvaluator.TryValidate(
                            step.Args,
                            stepPath + "/args",
                            operationDescriptor,
                            errors);
                        if (argsValidationFailure is not null)
                        {
                            return argsValidationFailure;
                        }
                    }

                    if (authorizationOperationsByName != null)
                    {
                        await ValidateReferencedOperationAsync(
                                operationName,
                                operationPath,
                                isImplicitEditOperation: false,
                                authorizationOperationsByName,
                                authorizationCache,
                                config,
                                errors,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    break;

                case IpcExecuteStepKind.Edit:
                    if (!RequestEditStepLowerPreviewBuilder.TryBuild(
                            step.EditContract,
                            request.AllowPlayMode,
                            out var operationNames,
                            out var errorMessage))
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorCodes.EditStepInvalid,
                            errorMessage,
                            stepPath));
                        continue;
                    }

                    if (authorizationOperationsByName == null)
                    {
                        break;
                    }

                    var uniqueOperationNames = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var requiredOperationName in operationNames)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!uniqueOperationNames.Add(requiredOperationName))
                        {
                            continue;
                        }

                        await ValidateReferencedOperationAsync(
                                requiredOperationName,
                                stepPath,
                                isImplicitEditOperation: true,
                                authorizationOperationsByName,
                                authorizationCache,
                                config,
                                errors,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Normalized request step at '{stepPath}' has unsupported kind '{step.Kind}'.");
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Invalid(errors);
    }

    private async ValueTask ValidateReferencedOperationAsync (
        string operationName,
        string instancePath,
        bool isImplicitEditOperation,
        IReadOnlyDictionary<string, UcliOperationAuthorizationDescriptor> operationsByName,
        IDictionary<string, OperationAuthorizationResult> authorizationCache,
        UcliConfig config,
        ICollection<ValidationError> errors,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);
        ArgumentNullException.ThrowIfNull(operationsByName);
        ArgumentNullException.ThrowIfNull(authorizationCache);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(errors);

        if (!operationsByName.TryGetValue(operationName, out var descriptor))
        {
            var message = isImplicitEditOperation
                ? $"Edit step requires operation '{operationName}', but it is not registered."
                : $"Operation '{operationName}' is not registered.";
            errors.Add(new ValidationError(
                ValidationErrorCodes.OperationNotFound,
                message,
                instancePath));
            return;
        }

        if (TryCreateExposureValidationError(
                descriptor,
                instancePath,
                isImplicitEditOperation,
                out var exposureError))
        {
            errors.Add(exposureError!);
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
                ? $"Edit step requires operation '{operationName}'. {authorizationResult.Message}"
                : authorizationResult.Message;
            errors.Add(new ValidationError(
                authorizationResult.ErrorCode ?? OperationAuthorizationErrorCodes.OperationNotAllowed,
                message,
                instancePath));
        }
    }

    private static bool TryCreateExposureValidationError (
        UcliOperationAuthorizationDescriptor operation,
        string instancePath,
        bool isImplicitEditOperation,
        out ValidationError? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);

        if (operation.Exposure == UcliOperationExposure.Public
            || (operation.Exposure == UcliOperationExposure.EditLoweringOnly && isImplicitEditOperation))
        {
            error = null;
            return false;
        }

        var message = operation.Exposure == UcliOperationExposure.EditLoweringOnly
            ? $"Operation '{operation.Name}' is available only through edit lowering."
            : $"Operation '{operation.Name}' has unsupported exposure '{operation.Exposure}'.";
        if (isImplicitEditOperation)
        {
            message = $"Edit step requires operation '{operation.Name}'. {message}";
        }

        error = new ValidationError(
            UcliCoreErrorCodes.InvalidArgument,
            message,
            instancePath);
        return true;
    }
}
