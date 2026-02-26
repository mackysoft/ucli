using MackySoft.Ucli.Configuration;

namespace MackySoft.Ucli.Operations;

/// <summary> Performs static pre-execution validation for JSON request structures. </summary>
internal interface IRequestStaticValidator
{
    /// <summary> Asynchronously validates one normalized request against structure and operation authorization constraints. </summary>
    /// <param name="request"> The normalized request. </param>
    /// <param name="config"> The configuration values used for operation authorization checks. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the aggregated validation result. </returns>
    ValueTask<ValidationResult> Validate (
        ValidateRequest request,
        UcliConfig config,
        CancellationToken cancellationToken = default);
}