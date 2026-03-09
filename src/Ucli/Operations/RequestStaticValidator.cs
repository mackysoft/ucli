using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Operations;

/// <summary> Performs static request validation for protocol, structure, and operation authorization. </summary>
internal sealed class RequestStaticValidator : IRequestStaticValidator
{
    private readonly IOperationCatalog operationCatalog;

    private readonly IOperationAuthorizationService operationAuthorizationService;

    /// <summary> Initializes a new instance of the <see cref="RequestStaticValidator" /> class. </summary>
    /// <param name="operationCatalog"> The operation catalog dependency. </param>
    /// <param name="operationAuthorizationService"> The operation authorization dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationCatalog" /> or <paramref name="operationAuthorizationService" /> is <see langword="null" />. </exception>
    public RequestStaticValidator (
        IOperationCatalog operationCatalog,
        IOperationAuthorizationService operationAuthorizationService)
    {
        this.operationCatalog = operationCatalog ?? throw new ArgumentNullException(nameof(operationCatalog));
        this.operationAuthorizationService = operationAuthorizationService ?? throw new ArgumentNullException(nameof(operationAuthorizationService));
    }

    /// <summary> Asynchronously validates one normalized request against structure and operation authorization constraints. </summary>
    /// <param name="request"> The normalized request. </param>
    /// <param name="unityProject"> The resolved Unity project context used to read project-scoped operation metadata. </param>
    /// <param name="config"> The configuration values used for operation authorization checks. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the aggregated validation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" />, <paramref name="unityProject" />, or <paramref name="config" /> is <see langword="null" />. </exception>
    public async ValueTask<ValidationResult> Validate (
        ValidateRequest request,
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<ValidationError>();
        if (request.ProtocolVersion != IpcProtocol.CurrentVersion)
        {
            errors.Add(new ValidationError(
                Code: ValidationErrorCodes.ProtocolVersionMismatch,
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

        if (request.Ops is null || request.Ops.Count == 0)
        {
            errors.Add(new ValidationError(
                Code: ValidationErrorCodes.OpsRequired,
                Message: "ops must contain at least one operation.",
                OpId: null));
            return new ValidationResult(errors);
        }

        var operations = await operationCatalog.GetAll(unityProject, config, cancellationToken).ConfigureAwait(false);
        var operationsByName = new Dictionary<string, UcliOperationDescriptor>(operations.Count, StringComparer.Ordinal);
        for (var i = 0; i < operations.Count; i++)
        {
            var operationDescriptor = operations[i];
            cancellationToken.ThrowIfCancellationRequested();
            operationsByName[operationDescriptor.Name] = operationDescriptor;
        }

        var usedOpIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operationRequest in request.Ops)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (operationRequest is null)
            {
                errors.Add(new ValidationError(
                    Code: ValidationErrorCodes.OpIdRequired,
                    Message: "opId is required.",
                    OpId: null));
                errors.Add(new ValidationError(
                    Code: ValidationErrorCodes.OpNameRequired,
                    Message: "op is required.",
                    OpId: null));
                continue;
            }

            if (!StringValueNormalizer.TryTrimToNonEmpty(operationRequest.OpId, out var normalizedOpId))
            {
                errors.Add(new ValidationError(
                    Code: ValidationErrorCodes.OpIdRequired,
                    Message: "opId is required.",
                    OpId: null));
            }
            else if (!usedOpIds.Add(normalizedOpId))
            {
                errors.Add(new ValidationError(
                    Code: ValidationErrorCodes.OpIdDuplicated,
                    Message: $"opId '{normalizedOpId}' is duplicated.",
                    OpId: normalizedOpId));
            }

            if (!StringValueNormalizer.TryTrimToNonEmpty(operationRequest.Op, out var normalizedOperationName))
            {
                errors.Add(new ValidationError(
                    Code: ValidationErrorCodes.OpNameRequired,
                    Message: "op is required.",
                    OpId: normalizedOpId));
                continue;
            }

            if (!operationsByName.TryGetValue(normalizedOperationName, out var descriptor))
            {
                errors.Add(new ValidationError(
                    Code: ValidationErrorCodes.OperationNotFound,
                    Message: $"Operation '{normalizedOperationName}' is not registered.",
                    OpId: normalizedOpId));
                continue;
            }

            var authorizationResult = await operationAuthorizationService
                .Authorize(descriptor, config, cancellationToken)
                .ConfigureAwait(false);
            if (!authorizationResult.IsAllowed)
            {
                errors.Add(new ValidationError(
                    Code: authorizationResult.ErrorCode ?? ValidationErrorCodes.OperationNotAllowed,
                    Message: authorizationResult.Message,
                    OpId: normalizedOpId));
            }
        }

        return new ValidationResult(errors);
    }
}