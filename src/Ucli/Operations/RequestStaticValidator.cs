using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Configuration;

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
    /// <param name="config"> The configuration values used for operation authorization checks. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the aggregated validation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> or <paramref name="config" /> is <see langword="null" />. </exception>
    public async ValueTask<ValidationResult> Validate (
        ValidateRequest request,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<ValidationError>();
        if (request.ProtocolVersion != CliProtocol.CurrentVersion)
        {
            errors.Add(new ValidationError(
                Code: ValidationErrorCodes.ProtocolVersionMismatch,
                Message: $"protocolVersion must be {CliProtocol.CurrentVersion}. Actual: {request.ProtocolVersion}.",
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

            var normalizedOpId = Normalize(operationRequest.OpId);
            if (normalizedOpId is null)
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

            var normalizedOperationName = Normalize(operationRequest.Op);
            if (normalizedOperationName is null)
            {
                errors.Add(new ValidationError(
                    Code: ValidationErrorCodes.OpNameRequired,
                    Message: "op is required.",
                    OpId: normalizedOpId));
                continue;
            }

            var descriptor = await operationCatalog.Get(normalizedOperationName, cancellationToken).ConfigureAwait(false);
            if (descriptor is null)
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

    /// <summary> Normalizes a string value by trimming whitespace and converting empty strings to <see langword="null" />. </summary>
    /// <param name="value"> The input value. </param>
    /// <returns> The normalized value, or <see langword="null" /> when empty. </returns>
    private static string? Normalize (string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
