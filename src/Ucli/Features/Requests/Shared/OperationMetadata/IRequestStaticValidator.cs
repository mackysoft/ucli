using MackySoft.Ucli.Shared.Configuration;

namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

/// <summary> Performs static pre-execution validation for JSON request structures. </summary>
internal interface IRequestStaticValidator
{
    /// <summary> Asynchronously validates one normalized request against structure and optional operation metadata. </summary>
    /// <param name="request"> The normalized request. </param>
    /// <param name="catalog"> The validation metadata catalog. When unavailable, metadata-dependent validation is skipped. </param>
    /// <param name="config"> The configuration values used for operation authorization checks. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the aggregated validation result. </returns>
    ValueTask<ValidationResult> Validate (
        ValidateRequest request,
        RequestStaticValidationCatalog catalog,
        UcliConfig config,
        CancellationToken cancellationToken = default);
}