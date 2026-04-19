using MackySoft.Ucli.Shared.Context;

namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

/// <summary> Resolves authoritative operation metadata and runs static request validation. </summary>
internal interface IRequestStaticValidationService
{
    /// <summary> Validates one request against authoritative operation metadata for the specified project context. </summary>
    /// <param name="request"> The normalized request. </param>
    /// <param name="projectContext"> The resolved project/config context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the aggregated validation result. </returns>
    ValueTask<ValidationResult> Validate (
        ValidateRequest request,
        ProjectContext projectContext,
        CancellationToken cancellationToken = default);
}